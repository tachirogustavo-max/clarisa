using Clarisa.Api.Http;
using Clarisa.Application.Features.Firmantes;

namespace Clarisa.Api.Endpoints;

public static class EndpointsFirmantes
{
    public static IEndpointRouteBuilder MapearFirmantes(this IEndpointRouteBuilder rutas)
    {
        var firmantes = rutas.MapGroup("/api/firmantes-boletin").RequireAuthorization(p => p.RequireRole("Administracion"));
        firmantes.MapGet("/", (HttpContext contexto, GestionFirmantes servicio, CancellationToken ct) =>
            servicio.ConsultarAsync(contexto.ActorActual(), ct));
        firmantes.MapPost("/", (CambiarFirmantes datos, HttpContext contexto, GestionFirmantes servicio, CancellationToken ct) =>
            servicio.CambiarAsync(contexto.ActorActual(), datos, ct));
        return rutas;
    }
}
