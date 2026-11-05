namespace Clarisa.Instalacion.Registro;

// Antes de crear, sustituir o borrar se rechaza cualquier tramo existente que sea punto de reparación (junction o enlace).
internal static class RutasSeguras
{
    public static void ExigirSinPuntosReparacion(string ruta)
    {
        var completa = Path.GetFullPath(ruta);
        var raiz = Path.GetPathRoot(completa) ?? throw new FalloRutaException("La ruta no tiene unidad.");
        var actual = raiz;
        foreach (var parte in completa[raiz.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            actual = Path.Combine(actual, parte);
            var info = new FileInfo(actual);
            if (!info.Exists && !Directory.Exists(actual)) return;
            if (File.GetAttributes(actual).HasFlag(FileAttributes.ReparsePoint))
                throw new FalloRutaException($"La ruta pasa por un enlace o junction no admitido: {actual}");
        }
    }

    public static bool Contiene(string padre, string hijo)
    {
        var a = Path.TrimEndingDirectorySeparator(Path.GetFullPath(padre)) + Path.DirectorySeparatorChar;
        var b = Path.TrimEndingDirectorySeparator(Path.GetFullPath(hijo)) + Path.DirectorySeparatorChar;
        return b.StartsWith(a, StringComparison.OrdinalIgnoreCase);
    }

    public static string Dentro(string raiz, string relativa)
    {
        if (Path.IsPathRooted(relativa) || relativa.Split('/', '\\').Any(p => p is ".." or "." or ""))
            throw new FalloRutaException($"Ruta relativa no admitida: {relativa}");
        var completa = Path.GetFullPath(Path.Combine(raiz, relativa.Replace('/', Path.DirectorySeparatorChar)));
        if (!Contiene(raiz, completa)) throw new FalloRutaException($"La ruta sale de su carpeta: {relativa}");
        return completa;
    }
}

internal sealed class FalloRutaException(string mensaje) : IOException(mensaje);
