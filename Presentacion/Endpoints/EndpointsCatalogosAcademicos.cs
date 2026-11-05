using Clarisa.Api.Http;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Resultados;
using Clarisa.Application.Features.AniosLectivos;
using Clarisa.Application.Features.Grados;
using Clarisa.Application.Features.Materias;
using Clarisa.Application.Features.Secciones;

namespace Clarisa.Api.Endpoints;

public static class EndpointsCatalogosAcademicos
{
    public static IEndpointRouteBuilder MapearCatalogosAcademicos(this IEndpointRouteBuilder rutas)
    {
        var anios = rutas.MapGroup("/api/anios-lectivos").RequireAuthorization(p => p.RequireRole("Administracion"));
        anios.MapGet("/", (HttpContext contexto, [AsParameters] ConsultaHttp consulta, GestionAniosLectivos servicio, CancellationToken ct) =>
            servicio.ListarAsync(contexto.ActorActual(), consulta.AConsulta(), ct));
        anios.MapGet("/{id:guid}", (Guid id, HttpContext contexto, GestionAniosLectivos servicio, CancellationToken ct) => servicio.ObtenerAsync(contexto.ActorActual(), id, ct));
        anios.MapPost("/", (CrearAnioLectivo datos, HttpContext contexto, GestionAniosLectivos servicio, CancellationToken ct) => servicio.CrearAsync(contexto.ActorActual(), datos, ct));
        anios.MapPut("/{id:guid}", (Guid id, ModificarAnioLectivo datos, HttpContext contexto, GestionAniosLectivos servicio, CancellationToken ct) =>
        {
            ActorHttp.ExigirId(id, datos.Id);
            return servicio.ModificarAsync(contexto.ActorActual(), datos, ct);
        });
        var materias = rutas.MapGroup("/api/materias").RequireAuthorization(p => p.RequireRole("Administracion"));
        materias.MapGet("/", (HttpContext contexto, [AsParameters] ConsultaHttp consulta, GestionMaterias servicio, CancellationToken ct) =>
            servicio.BuscarAsync(contexto.ActorActual(), consulta.AFiltro(), consulta.AConsulta(), ct));
        materias.MapPost("/", (CrearMateria datos, HttpContext contexto, GestionMaterias servicio, CancellationToken ct) => servicio.CrearAsync(contexto.ActorActual(), datos, ct));
        materias.MapPut("/{id:guid}", (Guid id, ModificarMateria datos, HttpContext contexto, GestionMaterias servicio, CancellationToken ct) =>
        {
            ActorHttp.ExigirId(id, datos.Id);
            return servicio.ModificarAsync(contexto.ActorActual(), datos, ct);
        });
        var grados = rutas.MapGroup("/api/grados").RequireAuthorization(p => p.RequireRole("Administracion"));
        grados.MapGet("/", (HttpContext contexto, [AsParameters] ConsultaHttp consulta, GestionGrados servicio, CancellationToken ct) =>
            servicio.BuscarAsync(contexto.ActorActual(), consulta.AFiltro(), consulta.AConsulta(), ct));
        grados.MapPost("/", (CrearGrado datos, HttpContext contexto, GestionGrados servicio, CancellationToken ct) => servicio.CrearAsync(contexto.ActorActual(), datos, ct));
        grados.MapPut("/{id:guid}", (Guid id, ModificarGrado datos, HttpContext contexto, GestionGrados servicio, CancellationToken ct) =>
        {
            ActorHttp.ExigirId(id, datos.Id);
            return servicio.ModificarAsync(contexto.ActorActual(), datos, ct);
        });
        var secciones = rutas.MapGroup("/api/secciones").RequireAuthorization(p => p.RequireRole("Administracion"));
        secciones.MapGet("/", (HttpContext contexto, [AsParameters] ConsultaHttp consulta, GestionSecciones servicio, CancellationToken ct) =>
            servicio.ListarPorAnioAsync(contexto.ActorActual(), consulta.AnioLectivoId ?? throw Error.DatosInvalidos("Selecciona el año escolar.", "AnioLectivoId"), consulta.AConsulta(), ct));
        secciones.MapPost("/", (CrearSeccion datos, HttpContext contexto, GestionSecciones servicio, CancellationToken ct) => servicio.CrearAsync(contexto.ActorActual(), datos, ct));
        secciones.MapPut("/{id:guid}", (Guid id, ModificarSeccion datos, HttpContext contexto, GestionSecciones servicio, CancellationToken ct) =>
        {
            ActorHttp.ExigirId(id, datos.Id);
            return servicio.ModificarAsync(contexto.ActorActual(), datos, ct);
        });
        secciones.MapDelete("/{id:guid}", async (Guid id, long versionEsperada, HttpContext contexto, GestionSecciones servicio, CancellationToken ct) =>
        {
            await servicio.EliminarAsync(contexto.ActorActual(), new EliminarRegistro(id, versionEsperada), ct);
            return Results.NoContent();
        });
        return rutas.MapearMateriasEvaluadas();
    }
}
