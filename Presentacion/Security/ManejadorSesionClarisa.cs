using System.Text.Encodings.Web;
using Clarisa.Application.Features.Cuentas;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Clarisa.Api.Security;

// Valida cada solicitud contra la sesión persistida: revocación, caducidad con el reloj del servidor, cuenta y docente activos.
// Un fallo del almacén no se convierte en 401: se propaga para que el manejador de errores responda 503.
public sealed class ManejadorSesionClarisa(IOptionsMonitor<AuthenticationSchemeOptions> opciones, ILoggerFactory registro,
    UrlEncoder codificador, AccesoCuentas acceso, IHostEnvironment entorno, OpcionesSeguridad seguridad)
    : AuthenticationHandler<AuthenticationSchemeOptions>(opciones, registro, codificador)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!CookieSesion.TransportePermitido(Context, entorno, seguridad)) return AuthenticateResult.NoResult();
        var token = CookieSesion.Leer(Context);
        if (string.IsNullOrEmpty(token)) return AuthenticateResult.NoResult();
        var identidad = await acceso.ValidarAsync(token, Context.RequestAborted);
        if (identidad is null) return AuthenticateResult.Fail("La sesión no es válida o terminó.");
        return AuthenticateResult.Success(new AuthenticationTicket(PrincipalSesion.Crear(identidad, Scheme.Name), Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties propiedades) =>
        ResponderAsync(StatusCodes.Status401Unauthorized, "SesionNoValida", "Tu sesión terminó o no es válida. Inicia sesión nuevamente.");

    protected override Task HandleForbiddenAsync(AuthenticationProperties propiedades) =>
        ResponderAsync(StatusCodes.Status403Forbidden, "AccesoDenegado", "No tiene permiso para realizar esta operación.");

    private Task ResponderAsync(int estado, string tipo, string mensaje)
    {
        Response.StatusCode = estado;
        return Response.WriteAsJsonAsync(new { Tipo = tipo, Mensaje = mensaje, Referencia = Context.TraceIdentifier },
            Context.RequestAborted);
    }
}
