namespace Clarisa.Api.Endpoints;

public static class RutasClarisa
{
    // Únicas rutas atendidas sin sesión: iniciar sesión y cerrarla (idempotente, solo revoca la cookie recibida). Cualquier otra,
    // incluidas las futuras sin marca, queda bajo la política de reserva que exige usuario autenticado.
    public static readonly IReadOnlySet<string> Anonimas = new HashSet<string>(StringComparer.Ordinal)
    {
        "POST /api/sesiones/", "DELETE /api/sesiones/"
    };

    public static IEndpointRouteBuilder MapearRutasClarisa(this IEndpointRouteBuilder rutas) =>
        rutas.MapearSesiones().MapearUsuarios().MapearPersonas().MapearCatalogosAcademicos().MapearNotasCalendario().MapearRelaciones()
            .MapearLecturas().MapearReportes().MapearClavePropia().MapearRespaldos().MapearResultadosAcademicos()
            .MapearPoliticasAcademicas().MapearFirmantes();
}
