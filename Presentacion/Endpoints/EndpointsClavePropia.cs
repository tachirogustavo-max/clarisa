using Clarisa.Api.Security;
using Clarisa.Api.Http;
using Clarisa.Application.Features.Cuentas;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace Clarisa.Api.Endpoints;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CredencialesCambioClave(string? ClaveActual, string? ClaveNueva)
{
    public override string ToString() => "Cambio de contraseña protegido";
}

public static class EndpointsClavePropia
{
    public static IEndpointRouteBuilder MapearClavePropia(this IEndpointRouteBuilder rutas)
    {
        rutas.MapPost("/api/cuenta/clave", async (CredencialesCambioClave solicitud, HttpContext contexto, CambioClavePropia cambio, CancellationToken ct) =>
        {
            await cambio.CambiarAsync(contexto.ActorActual(), new(solicitud.ClaveActual, solicitud.ClaveNueva), contexto.Connection.RemoteIpAddress?.ToString() ?? "", ct);
            CookieSesion.Eliminar(contexto);
            return Results.NoContent();
        }).RequireAuthorization().WithMetadata(new RequestSizeLimitAttribute(16 * 1024));
        return rutas;
    }
}
