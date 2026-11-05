namespace Clarisa.Infrastructure.Respaldos;

// Temporal único en la misma carpeta, volcado a disco y renombrado: el destino queda con el contenido anterior completo o el nuevo
// completo. Un temporal huérfano tras un corte no sustituye nada y se ignora por su extensión.
internal static class EscrituraAtomica
{
    public const string ExtensionTemporal = ".escribiendo";
    private const int CompartidoNoPermitido = unchecked((int)0x80070020);
    private const int Intentos = 10;

    public static void Reemplazar(string destino, ReadOnlySpan<byte> contenido) => Escribir(destino, contenido, sobrescribir: true);

    // Devuelve false si el destino ya existía; en ese caso no se modifica.
    public static bool Crear(string destino, ReadOnlySpan<byte> contenido) => Escribir(destino, contenido, sobrescribir: false);

    // Lectura que no impide al escritor sustituir el archivo mientras tanto.
    public static byte[] Leer(string ruta)
    {
        using var flujo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var contenido = new byte[flujo.Length];
        flujo.ReadExactly(contenido);
        return contenido;
    }

    private static bool Escribir(string destino, ReadOnlySpan<byte> contenido, bool sobrescribir)
    {
        var carpeta = Path.GetDirectoryName(Path.GetFullPath(destino))!;
        var temporal = Path.Combine(carpeta, $".{Path.GetFileName(destino)}.{Guid.NewGuid():N}{ExtensionTemporal}");
        try
        {
            using (var flujo = new FileStream(temporal, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                flujo.Write(contenido);
                flujo.Flush(flushToDisk: true);
            }
            if (!sobrescribir && File.Exists(destino)) return false;
            Mover(temporal, destino, sobrescribir);
            return true;
        }
        catch (IOException) when (!sobrescribir && File.Exists(destino))
        {
            return false;
        }
        finally
        {
            if (File.Exists(temporal)) File.Delete(temporal);
        }
    }

    // Otro proceso (antivirus, indexador, otra lectura) puede tener el destino abierto sin permitir su sustitución: se reintenta un
    // tiempo acotado y, si persiste, el error llega al llamador.
    private static void Mover(string temporal, string destino, bool sobrescribir)
    {
        for (var intento = 1; ; intento++)
        {
            try
            {
                File.Move(temporal, destino, sobrescribir);
                return;
            }
            catch (Exception error) when (intento < Intentos && (error is UnauthorizedAccessException
                                              || (error is IOException && error.HResult == CompartidoNoPermitido)))
            {
                Thread.Sleep(50 * intento);
            }
        }
    }
}
