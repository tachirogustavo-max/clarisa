using System.IO.Compression;
using System.Security.Cryptography;

namespace Clarisa.Infrastructure.Respaldos.Formato;

// Construye el ZIP en memoria aplicando los mismos límites que tendrá la lectura: no se produce una copia que luego no pueda leerse.
internal sealed class EscritorPaquete : IDisposable
{
    private readonly MemoryStream memoria = new();
    private readonly ZipArchive zip;
    private readonly LimitesPaquete limites;
    private long descomprimidos;

    public EscritorPaquete(LimitesPaquete limites)
    {
        this.limites = limites;
        zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true);
    }

    public EscrituraTabla Iniciar(TablaRespaldo tabla) => new(this, tabla, zip.CreateEntry(tabla.Entrada, CompressionLevel.Optimal));

    public byte[] Finalizar(Manifiesto manifiesto)
    {
        var contenido = manifiesto.Serializar();
        Contar(contenido.Length);
        using (var flujo = zip.CreateEntry(Manifiesto.Entrada, CompressionLevel.Optimal).Open()) flujo.Write(contenido);
        zip.Dispose();
        ExigirTamano();
        return memoria.ToArray();
    }

    public void Dispose()
    {
        zip.Dispose();
        if (memoria.TryGetBuffer(out var usado)) CryptographicOperations.ZeroMemory(usado.AsSpan());
        memoria.Dispose();
    }

    private void Contar(long bytes)
    {
        descomprimidos += bytes;
        if (descomprimidos > limites.MaximoDescomprimidos) throw ErroresRespaldo.Excede($"{limites.MaximoDescomprimidos / (1024 * 1024)} MiB sin comprimir");
    }

    private void ExigirTamano()
    {
        if (memoria.Length > limites.MaximoCifrado) throw ErroresRespaldo.Excede($"{limites.MaximoCifrado / (1024 * 1024)} MiB");
    }

    internal sealed class EscrituraTabla : IDisposable
    {
        private readonly EscritorPaquete paquete;
        private readonly TablaRespaldo tabla;
        private readonly Stream flujo;
        private readonly IncrementalHash huella = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private long filas;

        public EscrituraTabla(EscritorPaquete paquete, TablaRespaldo tabla, ZipArchiveEntry entrada)
        {
            this.paquete = paquete;
            this.tabla = tabla;
            flujo = entrada.Open();
        }

        public void Agregar(ReadOnlySpan<byte> linea)
        {
            if (++filas > paquete.limites.MaximoFilasPorTabla) throw ErroresRespaldo.Excede($"{paquete.limites.MaximoFilasPorTabla} filas por tabla");
            paquete.Contar(linea.Length + 1);
            flujo.Write(linea);
            flujo.WriteByte((byte)'\n');
            huella.AppendData(linea);
            huella.AppendData("\n"u8);
            if ((filas & 0x3FF) == 0) paquete.ExigirTamano();
        }

        public TablaManifiesto Terminar()
        {
            flujo.Dispose();
            paquete.ExigirTamano();
            return new(tabla.Nombre, tabla.Columnas.Select(c => c.Nombre).ToList(), filas, Convert.ToHexString(huella.GetHashAndReset()));
        }

        public void Dispose()
        {
            flujo.Dispose();
            huella.Dispose();
        }
    }
}

internal sealed record LimitesPaquete(long MaximoCifrado, long MaximoDescomprimidos, long MaximoFilasPorTabla);
