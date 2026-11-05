using Clarisa.Api.Http;
using Clarisa.Application.Common.Resultados;
using Clarisa.Application.Features.AsignacionesDocentes;
using Clarisa.Application.Features.Inscripciones;
using Clarisa.Application.Features.Boletines;

namespace Clarisa.Api.Endpoints;

public static class EndpointsRelaciones
{
    public static IEndpointRouteBuilder MapearRelaciones(this IEndpointRouteBuilder rutas)
    {
        var inscripciones = rutas.MapGroup("/api/inscripciones").RequireAuthorization(p => p.RequireRole("Administracion"));
        inscripciones.MapPost("/", (InscribirEstudiante datos, HttpContext contexto, GestionInscripciones servicio, CancellationToken ct) =>
            servicio.InscribirAsync(contexto.ActorActual(), datos, ct));
        inscripciones.MapPut("/{id:guid}", (Guid id, RetirarInscripcion datos, HttpContext contexto, GestionInscripciones servicio, CancellationToken ct) =>
        {
            ActorHttp.ExigirId(id, datos.Id);
            return servicio.RetirarAsync(contexto.ActorActual(), datos, ct);
        });
        rutas.MapGet("/api/secciones/{id:guid}/nomina", (Guid id, HttpContext contexto, GestionInscripciones servicio, CancellationToken ct) =>
            servicio.NominaAsync(contexto.ActorActual(), id, ct)).RequireAuthorization();
        var asignaciones = rutas.MapGroup("/api/asignaciones").RequireAuthorization(p => p.RequireRole("Administracion"));
        asignaciones.MapPost("/", (AsignarDocente datos, HttpContext contexto, GestionAsignacionesDocentes servicio, CancellationToken ct) =>
            servicio.AsignarAsync(contexto.ActorActual(), datos, ct));
        asignaciones.MapPut("/{id:guid}", (Guid id, CambiarDocente datos, HttpContext contexto, GestionAsignacionesDocentes servicio, CancellationToken ct) =>
        {
            ActorHttp.ExigirId(id, datos.Id);
            return servicio.CambiarDocenteAsync(contexto.ActorActual(), datos, ct);
        });
        asignaciones.MapDelete("/{id:guid}", async (Guid id, long versionEsperada, HttpContext contexto, GestionAsignacionesDocentes servicio, CancellationToken ct) =>
        {
            await servicio.RetirarAsync(contexto.ActorActual(), new EliminarRegistro(id, versionEsperada), ct);
            return Results.NoContent();
        });
        rutas.MapGet("/api/boletines/{inscripcionId:guid}", (Guid inscripcionId, HttpContext contexto, GestionBoletines servicio, CancellationToken ct) =>
            servicio.BoletinAsync(contexto.ActorActual(), inscripcionId, ct)).RequireAuthorization(p => p.RequireRole("Administracion"));
        rutas.MapGet("/api/boletines", (Guid anioLectivoId, Guid seccionId, Guid estudianteId, Guid? lapsoId,
            HttpContext contexto, GestionBoletines servicio, CancellationToken ct) =>
            servicio.BoletinAsync(contexto.ActorActual(), new ConsultarBoletin(anioLectivoId, seccionId, estudianteId, lapsoId), ct))
            .RequireAuthorization(p => p.RequireRole("Administracion"));
        return rutas;
    }
}
