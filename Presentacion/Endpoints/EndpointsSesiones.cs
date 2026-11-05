using System.Globalization;
using Clarisa.Api.Security;
using Clarisa.Application.Features.Cuentas;
using Microsoft.AspNetCore.Mvc;

namespace Clarisa.Api.Endpoints;

public sealed record CredencialesSesion(string? Usuario, string? Clave)
{
    public override string ToString() => "Credenciales de acceso protegidas";
}

public static class EndpointsSesiones
{
    public const long LimiteCuerpo = 16 * 1024;

    public static IEndpointRouteBuilder MapearSesiones(this IEndpointRouteBuilder rutas)
    {
        var sesiones = rutas.MapGroup("/api/sesiones").WithMetadata(new RequestSizeLimitAttribute(LimiteCuerpo));
        sesiones.MapPost("/", IniciarAsync).AllowAnonymous();
        sesiones.MapGet("/", (HttpContext contexto) => PrincipalSesion.Visible(contexto.User) is { } sesion
            ? Results.Ok(sesion)
            : Results.Unauthorized()).RequireAuthorization();
        sesiones.MapDelete("/", CerrarAsync).AllowAnonymous();
        return rutas;
    }

    private static async Task<IResult> IniciarAsync(HttpContext contexto, CredencialesSesion? credenciales, AccesoCuentas acceso,
        IHostEnvironment entorno, OpcionesSeguridad seguridad, ILoggerFactory registros, CancellationToken ct)
    {
        if (!CookieSesion.TransportePermitido(contexto, entorno, seguridad))
            return Rechazo(StatusCodes.Status400BadRequest, "TransporteNoSeguro", "Inicia sesión mediante HTTPS.");
        var origen = contexto.Connection.RemoteIpAddress?.ToString() ?? "";
        var resultado = await acceso.IniciarAsync(new IniciarSesion(credenciales?.Usuario, credenciales?.Clave, origen), ct);
        var registro = registros.CreateLogger("Clarisa.Api.Sesiones");
        switch (resultado.Estado)
        {
            case EstadoInicioSesion.Iniciada when resultado.Sesion is { } sesion:
                CookieSesion.Escribir(contexto, sesion.Token);
                registro.LogInformation("Sesión iniciada {SesionId} desde {Origen}.", sesion.SesionId, origen);
                return Results.Ok(new SesionVisible(sesion.Nombre, sesion.Perfil, sesion.Caduca, sesion.Usuario));
            case EstadoInicioSesion.Bloqueada:
                var segundos = (int)Math.Ceiling((resultado.ReintentarEn ?? TimeSpan.FromMinutes(1)).TotalSeconds);
                contexto.Response.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);
                registro.LogWarning("Inicio de sesión limitado por intentos desde {Origen}.", origen);
                return Rechazo(StatusCodes.Status429TooManyRequests, "DemasiadosIntentos",
                    "Se hicieron demasiados intentos. Espera unos minutos antes de volver a intentar.");
            default:
                registro.LogInformation("Inicio de sesión rechazado desde {Origen}.", origen);
                return Rechazo(StatusCodes.Status401Unauthorized, "CredencialesInvalidas", "El usuario o la contraseña no son correctos.");
        }
    }

    private static async Task<IResult> CerrarAsync(HttpContext contexto, AccesoCuentas acceso, CancellationToken ct)
    {
        await acceso.CerrarAsync(CookieSesion.Leer(contexto), ct);
        CookieSesion.Eliminar(contexto);
        return Results.NoContent();
    }

    private static IResult Rechazo(int estado, string tipo, string mensaje) =>
        Results.Json(new { Tipo = tipo, Mensaje = mensaje }, statusCode: estado);
}
