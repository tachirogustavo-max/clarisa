using Clarisa.Api.Http;
using Clarisa.Api.Security;
using Clarisa.Application.Abstractions.Seguridad;
using Microsoft.AspNetCore.Routing;

namespace Clarisa.Api.Configuration;

public static class TransporteClarisa
{
    public static IServiceCollection AgregarTransporteClarisa(this IServiceCollection servicios)
    {
        servicios.AddHttpContextAccessor();
        servicios.AddScoped<IReferenciaSesionActual, ReferenciaSesionHttp>();
        servicios.AddExceptionHandler<ErroresHttp>();
        servicios.AddProblemDetails();
        servicios.Configure<RouteHandlerOptions>(opciones => opciones.ThrowOnBadRequest = true);
        servicios.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(opciones =>
        {
            opciones.Limits.MaxRequestBodySize = 256 * 1024;
            opciones.Limits.MaxConcurrentConnections = 100;
            opciones.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
            opciones.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);
        });
        return servicios;
    }
    public static IApplicationBuilder UsarProteccionClarisa(this IApplicationBuilder aplicacion) =>
        aplicacion.UseMiddleware<ProteccionHttp>();
}
