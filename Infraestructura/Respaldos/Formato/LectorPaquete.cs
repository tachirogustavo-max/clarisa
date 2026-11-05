using System.IO.Compression;
using System.Security.Cryptography;

namespace Clarisa.Infrastructure.Respaldos.Formato;

// Lee el ZIP ya autenticado sin extraer nada a disco y solo por nombres exactos del catálogo. Los tamaños declarados por el ZIP no se
// creen: se cuentan los bytes realmente descomprimidos contra un presupuesto común.
internal sealed class LectorPaquete : IDisposable
{
    private readonly ZipArchive zip;
    private readonly LimitesPaquete limites;
    private long descomprimidos;

    private LectorPaquete(ZipArchive zip, LimitesPaquete limites)
    {
        this.zip = zip;
        this.limites = limites;
    }

    public Manifiesto Manifiesto { get; private set; } = null!;

    public static LectorPaquete Abrir(byte[] plano, LimitesPaquete limites)
    {
        ZipArchive zip;
        try
        {
            zip = new ZipArchive(new MemoryStream(plano, writable: false), ZipArchiveMode.Read);
        }
        catch (InvalidDataException error)
        {
            throw ErroresRespaldo.CopiaDanada("el contenido no es un paquete válido.", error);
        }
        var lector = new LectorPaquete(zip, limites);
        try
        {
            lector.ExigirEntradas();
            lector.Manifiesto = Manifiesto.Leer(lector.LeerManifiesto());
            return lector;
        }
        catch
        {
            lector.Dispose();
            throw;
        }
    }

    public async Task ValidarTodoAsync(CancellationToken ct)
    {
        foreach (var tabla in CatalogoRespaldo.Tablas) await RecorrerAsync(tabla, null, ct);
    }

    // Cada tabla se puede recorrer una vez por lector; el presupuesto de descompresión se acumula entre tablas.
    public async Task RecorrerAsync(TablaRespaldo tabla, Func<object?[], ValueTask>? alFila, CancellationToken ct)
    {
        var declarada = Manifiesto.Tablas.Single(t => t.Nombre == tabla.Nombre);
        using var huella = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long filas = 0;
        try
        {
            await using var flujo = zip.GetEntry(tabla.Entrada)!.Open();
            var lineas = new LectorLineas(flujo, Contar);
            while (lineas.Siguiente() is { } linea)
            {
                ct.ThrowIfCancellationRequested();
                if (++filas > Math.Min(declarada.Filas, limites.MaximoFilasPorTabla))
                    throw ErroresRespaldo.CopiaDanada($"{tabla.Nombre}: más registros de los declarados o permitidos.");
                var valores = CodificacionFilas.Leer(linea.Span, tabla);
                huella.AppendData(linea.Span);
                huella.AppendData("\n"u8);
                if (alFila is not null) await alFila(valores);
            }
        }
        catch (InvalidDataException error)
        {
            throw ErroresRespaldo.CopiaDanada($"{tabla.Nombre}: datos comprimidos dañados.", error);
        }
        if (filas != declarada.Filas || Convert.ToHexString(huella.GetHashAndReset()) != declarada.Sha256)
            throw ErroresRespaldo.CopiaDanada($"{tabla.Nombre}: el contenido no coincide con el manifiesto.");
    }

    public void Dispose() => zip.Dispose();

    private void ExigirEntradas()
    {
        var esperadas = CatalogoRespaldo.Tablas.Select(t => t.Entrada).Append(Manifiesto.Entrada).ToHashSet(StringComparer.Ordinal);
        if (zip.Entries.Count != esperadas.Count) throw ErroresRespaldo.CopiaDanada("el paquete tiene entradas de más o de menos.");
        var vistas = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entrada in zip.Entries)
        {
            if (!esperadas.Contains(entrada.FullName) || !vistas.Add(entrada.FullName))
                throw ErroresRespaldo.CopiaDanada("el paquete contiene una entrada no prevista o repetida.");
        }
    }

    private byte[] LeerManifiesto()
    {
        using var flujo = zip.GetEntry(Manifiesto.Entrada)!.Open();
        using var destino = new MemoryStream();
        var bloque = new byte[16 * 1024];
        int leidos;
        try
        {
            while ((leidos = flujo.Read(bloque)) > 0)
            {
                Contar(leidos);
                if (destino.Length + leidos > Manifiesto.MaximoBytes) throw ErroresRespaldo.CopiaDanada("el manifiesto es demasiado grande.");
                destino.Write(bloque, 0, leidos);
            }
        }
        catch (InvalidDataException error)
        {
            throw ErroresRespaldo.CopiaDanada("el manifiesto está dañado.", error);
        }
        return destino.ToArray();
    }

    private void Contar(int bytes)
    {
        descomprimidos += bytes;
        if (descomprimidos > limites.MaximoDescomprimidos) throw ErroresRespaldo.Excede($"{limites.MaximoDescomprimidos / (1024 * 1024)} MiB sin comprimir");
    }
}

internal sealed class LectorLineas(Stream origen, Action<int> contar)
{
    private readonly byte[] bufer = new byte[CodificacionFilas.MaximoBytesLinea + 1];
    private int inicio;
    private int fin;

    // La memoria devuelta solo es válida hasta la siguiente llamada.
    public ReadOnlyMemory<byte>? Siguiente()
    {
        while (true)
        {
            var salto = Array.IndexOf(bufer, (byte)'\n', inicio, fin - inicio);
            if (salto >= 0)
            {
                var linea = bufer.AsMemory(inicio, salto - inicio);
                inicio = salto + 1;
                return linea;
            }
            if (inicio > 0)
            {
                Buffer.BlockCopy(bufer, inicio, bufer, 0, fin - inicio);
                fin -= inicio;
                inicio = 0;
            }
            if (fin == bufer.Length) throw ErroresRespaldo.CopiaDanada("un registro supera 64 KiB.");
            var leidos = origen.Read(bufer, fin, bufer.Length - fin);
            if (leidos == 0) return fin == 0 ? null : throw ErroresRespaldo.CopiaDanada("el último registro está incompleto.");
            contar(leidos);
            fin += leidos;
        }
    }
}
