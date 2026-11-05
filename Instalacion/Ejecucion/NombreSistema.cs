namespace Clarisa.Instalacion.Ejecucion;

// Nombre que ve el personal en Windows: Aplicaciones instaladas, servicios y accesos directos. Los identificadores técnicos (nombre
// del servicio, carpetas, claves del Registro, grupo y regla de firewall) siguen siendo «Clarisa Estudios»/«Clarisa.Estudios»: están
// en los equipos ya instalados y cambiarlos obligaría a mover datos y recrear servicios.
internal static class NombreSistema
{
    public const string Visible = "Sistema de Control Académico La Clarisa";
    public const string AccesoDirecto = Visible + ".lnk";
    // Nombre de los accesos directos hasta rc.cierre09; al actualizar se sustituyen por los nuevos.
    public const string AccesoDirectoAnterior = "Clarisa Estudios.lnk";
}
