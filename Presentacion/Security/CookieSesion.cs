using System.Net;

namespace Clarisa.Api.Security;

// En HTTPS la cookie usa el prefijo __Host-: el navegador o cliente la exige Secure, con Path=/ y sin Domain. HTTP solo se admite
// en desarrollo con ambos extremos en loopback, igual que la protección de transporte de la API.
public static class CookieSesion
{
    public const string NombreSeguro = "__Host-clarisa_sesion";
    public const string NombreLocal = "clarisa_sesion";

    public static bool TransportePermitido(HttpContext contexto, IHostEnvironment entorno, OpcionesSeguridad opciones) =>
        contexto.Request.IsHttps || (opciones.PermitirHttpLoopback && entorno.IsDevelopment() && AmbosExtremosLocales(contexto));

    public static string? Leer(HttpContext contexto) =>
        contexto.Request.Cookies[contexto.Request.IsHttps ? NombreSeguro : NombreLocal];

    // Sin Expires: desaparece al cerrar el cliente. La caducidad real la decide el servidor.
    public static void Escribir(HttpContext contexto, string token) =>
        contexto.Response.Cookies.Append(Nombre(contexto), token, Opciones(contexto));

    public static void Eliminar(HttpContext contexto) => contexto.Response.Cookies.Delete(Nombre(contexto), Opciones(contexto));

    private static string Nombre(HttpContext contexto) => contexto.Request.IsHttps ? NombreSeguro : NombreLocal;

    private static CookieOptions Opciones(HttpContext contexto) => new()
    {
        HttpOnly = true,
        Secure = contexto.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true
    };

    private static bool AmbosExtremosLocales(HttpContext contexto) =>
        contexto.Connection.LocalIpAddress is { } local && IPAddress.IsLoopback(local)
        && contexto.Connection.RemoteIpAddress is { } remoto && IPAddress.IsLoopback(remoto);
}
