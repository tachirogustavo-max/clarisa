namespace Clarisa.Instalacion.Contratos;

// Modo explícito de Setup: «--desinstalar <ruta absoluta de retirada.json>». La ruta no es secreta ni basta por sí sola: el motor
// comprueba la ACL de su carpeta, el estado de instalación vecino y cada artefacto antes de proponer nada.
public static class ArgumentosRetirada
{
    public const string Opcion = "--desinstalar";
    public const string NombreReferencia = "retirada.json";

    // null sin la opción. Con la opción, devuelve la solicitud o un problema legible; nunca ambos.
    public static SolicitudRetirada? Interpretar(IReadOnlyList<string> argumentos, out string? problema)
    {
        ArgumentNullException.ThrowIfNull(argumentos);
        problema = null;
        var posicion = argumentos.ToList().FindIndex(a => string.Equals(a, Opcion, StringComparison.OrdinalIgnoreCase));
        if (posicion < 0) return null;
        if (argumentos.Count != 2 || posicion != 0)
        {
            problema = $"Uso: {Opcion} \"<ruta de {NombreReferencia}>\". No se admiten otras opciones junto con la retirada.";
            return null;
        }
        var ruta = argumentos[1];
        if (string.IsNullOrWhiteSpace(ruta) || ruta.Length > 400 || !Path.IsPathFullyQualified(ruta) || ruta.Any(char.IsControl)
            || !string.Equals(Path.GetFileName(ruta), NombreReferencia, StringComparison.OrdinalIgnoreCase))
        {
            problema = $"La referencia de retirada debe ser la ruta completa de {NombreReferencia} registrada por el instalador.";
            return null;
        }
        return new SolicitudRetirada(Path.GetFullPath(ruta));
    }
}

public sealed record SolicitudRetirada(string Referencia);

public enum AccionRetirada
{
    SeRetira,
    SeConserva
}

// La descripción se muestra al operador: nunca lleva secretos.
public sealed record ElementoRetirada(AccionRetirada Accion, string Categoria, string Descripcion, string? Ubicacion);

public sealed record RevisionRetirada(
    Guid Id,
    ModoInstalacion Modo,
    string Version,
    IReadOnlyList<ElementoRetirada> Elementos,
    IReadOnlyList<string> Advertencias,
    IReadOnlyList<PasoPlaneado> Pasos,
    IReadOnlyList<ProblemaSolicitud> Problemas,
    string Referencia)
{
    public bool Valida => Problemas.Count == 0 && Pasos.Count > 0;
    public IEnumerable<ElementoRetirada> SeRetira => Elementos.Where(e => e.Accion == AccionRetirada.SeRetira);
    public IEnumerable<ElementoRetirada> SeConserva => Elementos.Where(e => e.Accion == AccionRetirada.SeConserva);
}

public enum EstadoRetirada
{
    Completada,
    // Las fases críticas no terminaron comprobadas: se conservan la herramienta y la entrada de Windows para reintentar.
    Incompleta,
    Fallida,
    Cancelada,
    Rechazada
}

public sealed record ResultadoRetirada(
    Guid EjecucionId,
    ModoInstalacion? Modo,
    EstadoRetirada Estado,
    IReadOnlyList<ResultadoPaso> Pasos,
    string? ArchivoRegistro,
    IReadOnlyList<string> Indicaciones)
{
    public bool Terminada => Estado == EstadoRetirada.Completada;
}
