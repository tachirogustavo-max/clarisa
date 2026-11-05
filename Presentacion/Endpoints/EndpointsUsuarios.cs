using Clarisa.Api.Http;
using Clarisa.Application.Features.Cuentas;
using Microsoft.AspNetCore.Mvc;

namespace Clarisa.Api.Endpoints;

// La autorización real está en GestionCuentas; la exigencia de rol aquí solo evita trabajo innecesario.
public static class EndpointsUsuarios
{
    public static IEndpointRouteBuilder MapearUsuarios(this IEndpointRouteBuilder rutas)
    {
        var usuarios = rutas.MapGroup("/api/usuarios")
            .RequireAuthorization(politica => politica.RequireRole("Administracion"))
            .WithMetadata(new RequestSizeLimitAttribute(EndpointsSesiones.LimiteCuerpo));
        usuarios.MapGet("/", (HttpContext contexto, [AsParameters] ConsultaHttp consulta, GestionCuentas servicio, CancellationToken ct) =>
            servicio.BuscarAsync(contexto.ActorActual(), consulta.AFiltro(), consulta.AConsulta(), ct));
        usuarios.MapGet("/{id:guid}", (Guid id, HttpContext contexto, GestionCuentas servicio, CancellationToken ct) =>
            servicio.ObtenerAsync(contexto.ActorActual(), id, ct));
        usuarios.MapPost("/", (CrearCuenta datos, HttpContext contexto, GestionCuentas servicio, CancellationToken ct) =>
            servicio.CrearAsync(contexto.ActorActual(), datos, ct));
        usuarios.MapPut("/{id:guid}", (Guid id, ModificarCuenta datos, HttpContext contexto, GestionCuentas servicio, CancellationToken ct) =>
        {
            ActorHttp.ExigirId(id, datos.Id);
            return servicio.ModificarAsync(contexto.ActorActual(), datos, ct);
        });
        return rutas;
    }
}
