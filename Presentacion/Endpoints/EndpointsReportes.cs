using Clarisa.Api.Http;
using Clarisa.Application.Features.Reportes;
using Clarisa.Application.Features.Boletines;

namespace Clarisa.Api.Endpoints;

public static class EndpointsReportes
{
    public static IEndpointRouteBuilder MapearReportes(this IEndpointRouteBuilder rutas)
    {
        rutas.MapGet("/api/boletines/{inscripcionId:guid}/pdf", async (Guid inscripcionId, Guid? lapsoId, HttpContext contexto, ExportacionBoletines servicio, CancellationToken ct) =>
            Results.File(await servicio.GenerarAsync(contexto.ActorActual(), inscripcionId, lapsoId, ct), "application/pdf", "boletin-informativo.pdf"))
            .RequireAuthorization(p => p.RequireRole("Administracion"));
        rutas.MapGet("/api/secciones/{seccionId:guid}/boletines/pdf", async (Guid seccionId, Guid? lapsoId, HttpContext contexto, ExportacionBoletines servicio, CancellationToken ct) =>
            Results.File(await servicio.GenerarSeccionAsync(contexto.ActorActual(), seccionId, lapsoId, ct), "application/pdf", "boletines-seccion.pdf"))
            .RequireAuthorization(p => p.RequireRole("Administracion"));
        rutas.MapGet("/api/reportes/academicos", (HttpContext contexto, ConsultaReportes servicio, CancellationToken ct,
            int? pagina, int? tamanio, string? texto, Guid? anioLectivoId, Guid? seccionId, Guid? estudianteId, Guid? lapsoId, Guid? materiaId, Guid? docenteId) =>
            servicio.ListarAsync(contexto.ActorActual(), new(anioLectivoId, seccionId, estudianteId, lapsoId, materiaId, docenteId, texto), new(pagina ?? 1, tamanio ?? 25), ct))
            .RequireAuthorization();
        rutas.MapGet("/api/reportes/academicos/pdf", async (HttpContext contexto, ExportacionReportes servicio, CancellationToken ct,
            string? texto, Guid? anioLectivoId, Guid? seccionId, Guid? estudianteId, Guid? lapsoId, Guid? materiaId, Guid? docenteId) =>
            Results.File(await servicio.GenerarAsync(contexto.ActorActual(), new(anioLectivoId, seccionId, estudianteId, lapsoId, materiaId, docenteId, texto), ct),
                "application/pdf", "reporte-calificaciones.pdf")).RequireAuthorization();
        return rutas;
    }
}
