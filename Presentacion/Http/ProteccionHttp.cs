using System.Net;

namespace Clarisa.Api.Http;

public sealed class ProteccionHttp(RequestDelegate siguiente, IWebHostEnvironment entorno)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        contexto.Response.Headers.CacheControl = "no-store";
        contexto.Response.Headers.XContentTypeOptions = "nosniff";
        contexto.Response.Headers["Referrer-Policy"] = "no-referrer";
        contexto.Response.Headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
        if (!contexto.Request.IsHttps && !(entorno.IsDevelopment() && EsLocal(contexto)))
        {
            await RechazarAsync(contexto, 400, "TransporteNoSeguro", "La estación debe conectarse al servidor mediante HTTPS.");
            return;
        }
        if (ModificaDatos(contexto.Request.Method) && !OrigenPermitido(contexto.Request))
        {
            await RechazarAsync(contexto, 403, "OrigenNoPermitido", "La solicitud proviene de un origen no autorizado.");
            return;
        }
        await siguiente(contexto);
    }
    private static bool ModificaDatos(string metodo) =>
        !HttpMethods.IsGet(metodo) && !HttpMethods.IsHead(metodo) && !HttpMethods.IsOptions(metodo);

    private static bool OrigenPermitido(HttpRequest solicitud)
    {
        if (solicitud.Headers["Sec-Fetch-Site"].ToString() == "cross-site") return false;
        if (!solicitud.Headers.TryGetValue("Origin", out var origen)) return true;
        return origen.Count == 1 && Uri.TryCreate(origen[0], UriKind.Absolute, out var direccion)
            && direccion.Scheme == solicitud.Scheme
            && direccion.Authority.Equals(solicitud.Host.Value, StringComparison.OrdinalIgnoreCase)
            && direccion.AbsolutePath == "/" && direccion.Query.Length == 0 && direccion.Fragment.Length == 0
            && direccion.UserInfo.Length == 0;
    }
    private static bool EsLocal(HttpContext contexto) =>
        contexto.Connection.LocalIpAddress is { } local && IPAddress.IsLoopback(local)
        && contexto.Connection.RemoteIpAddress is { } remoto && IPAddress.IsLoopback(remoto);

    private static Task RechazarAsync(HttpContext contexto, int estado, string tipo, string mensaje)
    {
        contexto.Response.StatusCode = estado;
        return contexto.Response.WriteAsJsonAsync(new { Tipo = tipo, Mensaje = mensaje, Referencia = contexto.TraceIdentifier }, contexto.RequestAborted);
    }
}
