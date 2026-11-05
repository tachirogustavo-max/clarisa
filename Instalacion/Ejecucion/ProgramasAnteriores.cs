namespace Clarisa.Instalacion.Ejecucion;

// Hasta la versión 1.0.0-rc.cierre08 los programas se llamaban Escolar.*. Una instalación de esas versiones los conserva con ese
// nombre hasta que la actualización los sustituye: aquí solo se reconocen como propios al actualizar y al retirar; nunca se instalan.
internal static class ProgramasAnteriores
{
    public const string Api = "Escolar.Api.exe";
    public const string Mantenimiento = "Escolar.Mantenimiento.exe";
    public const string Escritorio = "Escolar.Desktop.exe";
    public const string Herramienta = "Escolar.Setup.exe";

    public static bool MismoArchivo(string? ruta, params string[] candidatos) =>
        ruta is not null && Path.IsPathFullyQualified(ruta)
        && candidatos.Any(c => string.Equals(Path.GetFullPath(ruta), Path.GetFullPath(c), StringComparison.OrdinalIgnoreCase));
}
