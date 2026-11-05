using Clarisa.Api.Http;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Features.Calificaciones;
using Clarisa.Application.Features.Lapsos;
using Clarisa.Application.Features.VentanasCarga;

namespace Clarisa.Api.Endpoints;

public static class EndpointsNotasCalendario
{
    public static IEndpointRouteBuilder MapearNotasCalendario(this IEndpointRouteBuilder rutas)
    {
        var notas = rutas.MapGroup("/api/calificaciones").RequireAuthorization();
        notas.MapGet("/", (Guid seccionId, Guid materiaId, Guid lapsoId, HttpContext contexto, GestionCalificaciones servicio, CancellationToken ct) =>
            servicio.PlanillaAsync(contexto.ActorActual(), new(seccionId, materiaId, lapsoId), ct));
        notas.MapPost("/", (GuardarCalificaciones datos, HttpContext contexto, GestionCalificaciones servicio, CancellationToken ct) =>
            servicio.GuardarAsync(contexto.ActorActual(), datos, ct));
        var ventanas = rutas.MapGroup("/api/ventanas-carga").RequireAuthorization();
        ventanas.MapGet("/{id:guid}", (Guid id, HttpContext contexto, GestionVentanasCarga servicio, CancellationToken ct) =>
            servicio.ConsultarAsync(contexto.ActorActual(), id, ct));
        ventanas.MapPost("/abrir", (AbrirVentanaCarga datos, HttpContext contexto, GestionVentanasCarga servicio, CancellationToken ct) =>
            servicio.AbrirAsync(contexto.ActorActual(), datos, ct)).RequireAuthorization(p => p.RequireRole("Administracion"));
        ventanas.MapPost("/cerrar", (CerrarVentanaCarga datos, HttpContext contexto, GestionVentanasCarga servicio, CancellationToken ct) =>
            servicio.CerrarAsync(contexto.ActorActual(), datos, ct)).RequireAuthorization(p => p.RequireRole("Administracion"));
        var lapsos = rutas.MapGroup("/api/lapsos").RequireAuthorization(p => p.RequireRole("Administracion"));
        lapsos.MapGet("/", (HttpContext contexto, [AsParameters] ConsultaHttp consulta, ConsultaCalendario servicio, CancellationToken ct) =>
            servicio.ListarAsync(contexto.ActorActual(), consulta.AnioLectivoId ?? throw Error.DatosInvalidos("Selecciona el año escolar."), consulta.AConsulta(), ct));
        lapsos.MapPost("/", (CrearLapso datos, HttpContext contexto, GestionLapsos servicio, CancellationToken ct) => servicio.CrearAsync(contexto.ActorActual(), datos, ct));
        lapsos.MapPut("/{id:guid}", (Guid id, ModificarLapso datos, HttpContext contexto, GestionLapsos servicio, CancellationToken ct) =>
        {
            ActorHttp.ExigirId(id, datos.Id);
            return servicio.ModificarAsync(contexto.ActorActual(), datos, ct);
        });
        rutas.MapGet("/api/anios-lectivos/{id:guid}/carga-notas", (Guid id, HttpContext contexto, ConsultaCalendario servicio, CancellationToken ct) =>
            servicio.CargaAsync(contexto.ActorActual(), id, ct)).RequireAuthorization(p => p.RequireRole("Administracion"));
        rutas.MapPut("/api/anios-lectivos/{id:guid}/lapso-actual", (Guid id, SeleccionarLapsoActual datos, HttpContext contexto, GestionLapsos servicio, CancellationToken ct) =>
        {
            ActorHttp.ExigirId(id, datos.AnioLectivoId);
            return servicio.SeleccionarActualAsync(contexto.ActorActual(), datos, ct);
        }).RequireAuthorization(p => p.RequireRole("Administracion"));
        return rutas;
    }
}
