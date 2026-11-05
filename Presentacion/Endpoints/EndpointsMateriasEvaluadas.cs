using Clarisa.Api.Http;
using Clarisa.Application.Features.Materias;
using Microsoft.AspNetCore.Mvc;

namespace Clarisa.Api.Endpoints;
public static class EndpointsMateriasEvaluadas
{
    public static IEndpointRouteBuilder MapearMateriasEvaluadas(this IEndpointRouteBuilder rutas)
    {
        rutas.MapPost("/api/materias/evaluadas", (HttpContext contexto, CrearMateriaEvaluada solicitud,
            CreacionMateriaEvaluada servicio, CancellationToken ct) => servicio.CrearAsync(contexto.ActorActual(), solicitud, ct))
            .RequireAuthorization(p => p.RequireRole("Administracion"))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));
        return rutas;
    }
}
