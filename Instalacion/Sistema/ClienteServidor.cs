using System.Net;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Clarisa.Instalacion.Contratos;

namespace Clarisa.Instalacion.Sistema;

internal sealed record RespuestaServidor(HttpStatusCode Estado, string Detalle)
{
    // El servidor respondió al inicio de sesión y negó el acceso (credenciales, permiso o demasiados intentos): no es un fallo técnico.
    public bool RechazoAcceso { get; init; }

    public static bool EsRechazoAcceso(HttpStatusCode estado) =>
        estado is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests;
}

internal interface IClienteServidor
{
    // confianza null: validación normal del sistema (estación). Con certificado: solo ese certificado como raíz (servidor recién creado).
    Task<RespuestaServidor> ConsultarSesionAnonimaAsync(Uri servidor, X509Certificate2? confianza, CancellationToken ct);
    Task<RespuestaServidor> IniciarYCerrarSesionAsync(Uri servidor, X509Certificate2? confianza, CredencialOperador credencial,
        CancellationToken ct);
}

internal sealed class ClienteServidorHttps : IClienteServidor
{
    // La conexión incluye resolver el nombre del servidor. En una red sin DNS propio (o justo después de cambiar el perfil de red),
    // Windows puede tardar varios segundos en resolver el nombre del equipo: medido en Windows Sandbox, unos 8 s durante el primer minuto.
    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan LimiteConexion = TimeSpan.FromSeconds(20);

    public async Task<RespuestaServidor> ConsultarSesionAnonimaAsync(Uri servidor, X509Certificate2? confianza, CancellationToken ct)
    {
        using var cliente = Crear(servidor, confianza, out _);
        using var respuesta = await cliente.GetAsync("api/sesiones", ct);
        return new RespuestaServidor(respuesta.StatusCode, $"GET /api/sesiones respondió {(int)respuesta.StatusCode}.");
    }

    public async Task<RespuestaServidor> IniciarYCerrarSesionAsync(Uri servidor, X509Certificate2? confianza,
        CredencialOperador credencial, CancellationToken ct)
    {
        using var cliente = Crear(servidor, confianza, out var cookies);
        using (var inicio = await cliente.PostAsJsonAsync("api/sesiones", new { credencial.Usuario, Clave = credencial.ClaveTexto() }, ct))
        {
            if (inicio.StatusCode != HttpStatusCode.OK)
                return new RespuestaServidor(inicio.StatusCode, $"El inicio de sesión respondió {(int)inicio.StatusCode}.")
                {
                    RechazoAcceso = RespuestaServidor.EsRechazoAcceso(inicio.StatusCode)
                };
        }
        HttpStatusCode consulta;
        using (var sesion = await cliente.GetAsync("api/sesiones", ct)) consulta = sesion.StatusCode;
        using var cierre = await cliente.DeleteAsync("api/sesiones", ct);
        cookies.GetAllCookies().Clear();
        return consulta == HttpStatusCode.OK && cierre.IsSuccessStatusCode
            ? new RespuestaServidor(HttpStatusCode.OK, "Inicio de sesión, consulta autenticada y cierre correctos.")
            : new RespuestaServidor(consulta, $"Sesión iniciada pero la consulta respondió {(int)consulta} y el cierre {(int)cierre.StatusCode}.");
    }

    private static HttpClient Crear(Uri servidor, X509Certificate2? confianza, out CookieContainer cookies)
    {
        cookies = new CookieContainer();
        var manejador = new SocketsHttpHandler
        {
            CookieContainer = cookies,
            UseProxy = false,
            AllowAutoRedirect = false,
            ConnectTimeout = LimiteConexion
        };
        if (confianza is not null)
        {
            var raiz = confianza;
            manejador.SslOptions.RemoteCertificateValidationCallback = (_, certificado, _, errores) =>
                ValidarContra(raiz, certificado, errores);
        }
        return new HttpClient(manejador) { BaseAddress = servidor, Timeout = Limite };
    }

    // Se acepta solo el certificado generado por esta instalación y solo si el nombre coincide; nunca cualquier certificado.
    internal static bool ValidarContra(X509Certificate2 raiz, X509Certificate? recibido, SslPolicyErrors errores)
    {
        if (recibido is null || errores.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch)
                             || errores.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable)) return false;
        using var certificado = new X509Certificate2(recibido);
        using var cadena = new X509Chain();
        cadena.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        cadena.ChainPolicy.CustomTrustStore.Add(raiz);
        cadena.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return cadena.Build(certificado) && certificado.Thumbprint == raiz.Thumbprint;
    }
}
