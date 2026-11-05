using Clarisa.Api.Http;
using Clarisa.Application.Features.CierresResultados;
using Clarisa.Application.Features.PoliticasAcademicas;

namespace Clarisa.Api.Endpoints;

// Los casos de uso repiten la autorización; el rol aquí solo descarta pronto las peticiones ajenas.
public static class EndpointsPoliticasAcademicas
{
    public static IEndpointRouteBuilder MapearPoliticasAcademicas(this IEndpointRouteBuilder rutas)
    {
        rutas.MapGet("/api/anios-lectivos/{anioId:guid}/politicas", (Guid anioId, HttpContext contexto, GestionPoliticas servicio,
            CancellationToken ct) => servicio.ListarAsync(contexto.ActorActual(), anioId, ct)).RequireAuthorization(p => p.RequireRole("Administracion"));
        var politicas = rutas.MapGroup("/api/politicas-academicas").RequireAuthorization(p => p.RequireRole("Administracion"));
        politicas.MapPost("/", (CrearPolitica datos, HttpContext contexto, GestionPoliticas servicio, CancellationToken ct) =>
            servicio.CrearAsync(contexto.ActorActual(), datos, ct));
        politicas.MapPut("/{id:guid}", (Guid id, EditarPolitica datos, HttpContext contexto, GestionPoliticas servicio, CancellationToken ct) =>
        {
            ActorHttp.ExigirId(id, datos.Id);
            return servicio.EditarAsync(contexto.ActorActual(), datos, ct);
        });
        politicas.MapPost("/{id:guid}/activar", (Guid id, ActivarPolitica datos, HttpContext contexto, GestionPoliticas servicio,
            CancellationToken ct) =>
        {
            ActorHttp.ExigirId(id, datos.Id);
            return servicio.ActivarAsync(contexto.ActorActual(), datos, ct);
        });

        rutas.MapGet("/api/secciones/{seccionId:guid}/cierres", (Guid seccionId, HttpContext contexto, GestionCierres servicio,
            CancellationToken ct) => servicio.ListarAsync(contexto.ActorActual(), seccionId, ct)).RequireAuthorization(p => p.RequireRole("Administracion"));
        var cierres = rutas.MapGroup("/api/cierres-resultados").RequireAuthorization(p => p.RequireRole("Administracion"));
        cierres.MapPost("/", (CerrarResultados datos, HttpContext contexto, GestionCierres servicio, CancellationToken ct) =>
            servicio.CerrarAsync(contexto.ActorActual(), datos, ct));
        cierres.MapPost("/{id:guid}/anular", (Guid id, AnularCierre datos, HttpContext contexto, GestionCierres servicio, CancellationToken ct) =>
        {
            ActorHttp.ExigirId(id, datos.Id);
            return servicio.AnularAsync(contexto.ActorActual(), datos, ct);
        });
        return rutas;
    }
}
