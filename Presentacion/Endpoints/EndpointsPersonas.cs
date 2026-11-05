using Clarisa.Api.Http;
using Clarisa.Application.Features.Estudiantes;
using Clarisa.Application.Features.Docentes;

namespace Clarisa.Api.Endpoints;

public static class EndpointsPersonas
{
    public static IEndpointRouteBuilder MapearPersonas(this IEndpointRouteBuilder rutas)
    {
        var estudiantes = rutas.MapGroup("/api/estudiantes").RequireAuthorization(p => p.RequireRole("Administracion"));
        estudiantes.MapGet("/", (HttpContext contexto, [AsParameters] ConsultaHttp consulta, GestionEstudiantes servicio, CancellationToken ct) =>
            servicio.BuscarAsync(contexto.ActorActual(), consulta.AFiltro(), consulta.AConsulta(), ct));
        estudiantes.MapGet("/{id:guid}", (Guid id, HttpContext contexto, GestionEstudiantes servicio, CancellationToken ct) =>
            servicio.ObtenerAsync(contexto.ActorActual(), id, ct));
        estudiantes.MapPost("/", (CrearEstudiante datos, HttpContext contexto, GestionEstudiantes servicio, CancellationToken ct) =>
            servicio.CrearAsync(contexto.ActorActual(), datos, ct));
        estudiantes.MapPut("/{id:guid}", (Guid id, ModificarEstudiante datos, HttpContext contexto, GestionEstudiantes servicio, CancellationToken ct) =>
        {
            ActorHttp.ExigirId(id, datos.Id);
            return servicio.ModificarAsync(contexto.ActorActual(), datos, ct);
        });

        var docentes = rutas.MapGroup("/api/docentes").RequireAuthorization(p => p.RequireRole("Administracion"));
        docentes.MapGet("/", (HttpContext contexto, [AsParameters] ConsultaHttp consulta, GestionDocentes servicio, CancellationToken ct) =>
            servicio.BuscarAsync(contexto.ActorActual(), consulta.AFiltro(), consulta.AConsulta(), ct));
        docentes.MapGet("/{id:guid}", (Guid id, HttpContext contexto, GestionDocentes servicio, CancellationToken ct) =>
            servicio.ObtenerAsync(contexto.ActorActual(), id, ct));
        docentes.MapPost("/", (CrearDocente datos, HttpContext contexto, GestionDocentes servicio, CancellationToken ct) =>
            servicio.CrearAsync(contexto.ActorActual(), datos, ct));
        docentes.MapPut("/{id:guid}", (Guid id, ModificarDocente datos, HttpContext contexto, GestionDocentes servicio, CancellationToken ct) =>
        {
            ActorHttp.ExigirId(id, datos.Id);
            return servicio.ModificarAsync(contexto.ActorActual(), datos, ct);
        });
        return rutas;
    }
}
