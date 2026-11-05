using Clarisa.Api.Http;
using Clarisa.Application.Common.Resultados;
using Clarisa.Application.Features.Calificaciones;
using Clarisa.Application.Features.ConfiguracionAcademica;

namespace Clarisa.Api.Endpoints;

// Plan por grado, escala cualitativa y exoneraciones. Los permisos se vuelven a exigir en cada caso de uso.
public static class EndpointsResultadosAcademicos
{
    public static IEndpointRouteBuilder MapearResultadosAcademicos(this IEndpointRouteBuilder rutas)
    {
        rutas.MapGet("/api/anios-lectivos/{anioId:guid}/grados", (Guid anioId, HttpContext contexto, GestionPlanEstudios servicio, CancellationToken ct) =>
            servicio.GradosAsync(contexto.ActorActual(), anioId, ct)).RequireAuthorization(p => p.RequireRole("Administracion"));
        rutas.MapGet("/api/anios-lectivos/{anioId:guid}/escala-cualitativa", (Guid anioId, HttpContext contexto,
            GestionEscalaCualitativa servicio, CancellationToken ct) => servicio.ListarAsync(contexto.ActorActual(), anioId, ct))
            .RequireAuthorization();
        var escala = rutas.MapGroup("/api/escala-cualitativa").RequireAuthorization(p => p.RequireRole("Administracion"));
        escala.MapPost("/", (AgregarLetraCualitativa datos, HttpContext contexto, GestionEscalaCualitativa servicio, CancellationToken ct) =>
            servicio.AgregarAsync(contexto.ActorActual(), datos, ct));
        escala.MapDelete("/{id:guid}", async (Guid id, long versionEsperada, HttpContext contexto, GestionEscalaCualitativa servicio,
            CancellationToken ct) =>
        {
            await servicio.EliminarAsync(contexto.ActorActual(), new EliminarRegistro(id, versionEsperada), ct);
            return Results.NoContent();
        });

        rutas.MapGet("/api/anios-lectivos/{anioId:guid}/plan-materias", (Guid anioId, string? grado, HttpContext contexto,
            GestionPlanEstudios servicio, CancellationToken ct) => servicio.ListarAsync(contexto.ActorActual(), anioId, grado, ct))
            .RequireAuthorization(p => p.RequireRole("Administracion"));
        var plan = rutas.MapGroup("/api/plan-materias").RequireAuthorization(p => p.RequireRole("Administracion"));
        plan.MapPut("/{id:guid}", (Guid id, ConfigurarPlanMateria datos, HttpContext contexto, GestionPlanEstudios servicio,
            CancellationToken ct) =>
        {
            ActorHttp.ExigirId(id, datos.Id);
            return servicio.ConfigurarAsync(contexto.ActorActual(), datos, ct);
        });
        plan.MapDelete("/{id:guid}", async (Guid id, long versionEsperada, HttpContext contexto, GestionPlanEstudios servicio,
            CancellationToken ct) =>
        {
            await servicio.EliminarAsync(contexto.ActorActual(), new EliminarRegistro(id, versionEsperada), ct);
            return Results.NoContent();
        });

        var exoneraciones = rutas.MapGroup("/api/calificaciones/exoneraciones").RequireAuthorization(p => p.RequireRole("Administracion"));
        exoneraciones.MapPost("/", (Exonerar datos, HttpContext contexto, GestionExoneraciones servicio, CancellationToken ct) =>
            servicio.ExonerarAsync(contexto.ActorActual(), datos, ct));
        exoneraciones.MapPost("/revocar", (RevocarExoneracion datos, HttpContext contexto, GestionExoneraciones servicio,
            CancellationToken ct) => servicio.RevocarAsync(contexto.ActorActual(), datos, ct));
        return rutas;
    }
}
