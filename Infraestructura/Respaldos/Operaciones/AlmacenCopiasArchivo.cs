using System.Security.Cryptography;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Features.Respaldos;
using Clarisa.Infrastructure.Configuration;

namespace Clarisa.Infrastructure.Respaldos.Operaciones;

// Rutas derivadas solo del Id de la operación: la estación nunca aporta nombres ni rutas.
public sealed class AlmacenCopiasArchivo(ConfiguracionRespaldos configuracion) : IAlmacenCopias
{
    public const string ExtensionCopia = ".liceo-respaldo";
    private const string ExtensionTemporal = ".parcial";
    private const int Bloque = 81920;

    internal string Temporal(Guid id) => Path.Combine(configuracion.DirectorioTemporal, id.ToString("N") + ExtensionTemporal);

    internal string Publicada(Guid id) => Path.Combine(configuracion.DirectorioCopias, id.ToString("N") + ExtensionCopia);

    // Se exige espacio para una copia del tamaño máximo más el margen: una copia a medias por disco lleno nunca se publica, pero se
    // evita empezar una que casi seguro fallará.
    public bool HayEspacioSuficiente()
    {
        var unidad = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(configuracion.Directorio))!);
        return unidad.AvailableFreeSpace >= configuracion.MinimoBytesLibres + configuracion.MaximoBytesCifrado;
    }

    public Task PublicarAsync(Guid operacionId, CancellationToken ct)
    {
        try
        {
            File.Move(Temporal(operacionId), Publicada(operacionId), overwrite: false);
            return Task.CompletedTask;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw Error.AlmacenNoDisponible(error);
        }
    }

    // Si no se puede borrar ahora, la reconciliación del próximo arranque lo vuelve a intentar; un temporal nunca se publica ni se lista.
    public void DescartarTemporal(Guid operacionId)
    {
        try
        {
            File.Delete(Temporal(operacionId));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning("No se pudo eliminar un temporal de respaldo: " + error.GetType().Name);
        }
    }

    // Solo para copias automáticas antiguas. Un fallo se propaga: la operación no se marca retirada si su archivo sigue ahí.
    public void RetirarPublicada(Guid operacionId) => File.Delete(Publicada(operacionId));

    public void DescartarTemporalesHuerfanos()
    {
        Directory.CreateDirectory(configuracion.DirectorioTemporal);
        foreach (var archivo in Directory.EnumerateFiles(configuracion.DirectorioTemporal, "*" + ExtensionTemporal))
        {
            if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(archivo), "N", out var id)) DescartarTemporal(id);
        }
    }

    public async Task<bool> PublicadaCoincideAsync(Guid operacionId, long bytes, string huella, CancellationToken ct)
    {
        var ruta = Publicada(operacionId);
        if (!File.Exists(ruta) || new FileInfo(ruta).Length != bytes) return false;
        await using var flujo = Abrir(ruta);
        return Convert.ToHexString(await SHA256.HashDataAsync(flujo, ct)) == huella;
    }

    public async Task EnviarAsync(Guid operacionId, long bytes, string huella, EscritorBloques escritor, CancellationToken ct)
    {
        await using var flujo = Abrir(Publicada(operacionId));
        if (flujo.Length != bytes) throw NoCoincide();
        using var calculo = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var bufer = new byte[Bloque];
        int leidos;
        while ((leidos = await flujo.ReadAsync(bufer, ct)) > 0)
        {
            calculo.AppendData(bufer, 0, leidos);
            await escritor(bufer.AsMemory(0, leidos), ct);
        }
        // Detectado al final: el cliente recibe una transferencia interrumpida y no un archivo aparentemente completo.
        if (Convert.ToHexString(calculo.GetHashAndReset()) != huella) throw NoCoincide();
    }

    internal FileStream CrearTemporal(Guid operacionId)
    {
        Directory.CreateDirectory(configuracion.DirectorioTemporal);
        return new FileStream(Temporal(operacionId), FileMode.CreateNew, FileAccess.Write, FileShare.None, Bloque);
    }

    private static FileStream Abrir(string ruta)
    {
        try
        {
            return new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read, Bloque, FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch (FileNotFoundException)
        {
            throw Error.NoEncontrado("La copia no se encuentra en el servidor.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw Error.AlmacenNoDisponible(error);
        }
    }

    private static ErrorAplicacionException NoCoincide() =>
        new(TipoError.AlmacenNoDisponible, "El archivo de la copia en el servidor no coincide con el registrado. No se entregó como válido.");
}
