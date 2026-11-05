using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;

namespace Clarisa.Desktop.Services;
public sealed class ClienteSesiones : IClienteSesiones, IDisposable
{
    private readonly HttpClient cliente;
    internal HttpClient Transporte => cliente;

    public ClienteSesiones() : this(new HttpClientHandler { AllowAutoRedirect = false })
    {
    }

    public ClienteSesiones(HttpMessageHandler transporte)
    {
        cliente = new HttpClient(transporte)
        {
            // Cada solicitud fija su propio límite (15 s las comunes, hasta 3 min los PDF de una sección); este es solo el tope general.
            Timeout = TimeSpan.FromMinutes(4),
            MaxResponseContentBufferSize = 64 * 1024
        };
    }

    public async Task<ResultadoAcceso> IniciarAsync(Uri servidor, string usuario, SecureString clave, CancellationToken ct)
    {
        var destino = DireccionServidor.Validar(servidor.AbsoluteUri);
        if (destino.Error is not null)
            return new(null, destino.Error);
        var memoria = Marshal.SecureStringToGlobalAllocUnicode(clave);
        try
        {
            var credenciales = new
            {
                usuario,
                clave = Marshal.PtrToStringUni(memoria)
            };
            using var respuesta = await cliente.PostAsJsonAsync(new Uri(servidor, "api/sesiones"), credenciales, ct);
            if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
                return new(null, "El usuario o la contraseña no son correctos.");
            if ((int)respuesta.StatusCode == 429)
                return new(null, "Se hicieron varios intentos. Espera unos minutos antes de volver a entrar.");
            if (!respuesta.IsSuccessStatusCode)
                return new(null, "No se pudo iniciar sesión. Comprueba la dirección del servidor o consulta al administrador.");
            await respuesta.Content.LoadIntoBufferAsync(64 * 1024, ct);
            var sesion = await respuesta.Content.ReadFromJsonAsync<SesionActual>(ct);
            if (sesion is null || string.IsNullOrWhiteSpace(sesion.Nombre) || sesion.Perfil is not ("Administracion" or "Docente") || sesion.Caduca <= DateTimeOffset.UtcNow)
                return new(null, "El servidor no devolvió una sesión válida.");
            return new(sesion, null);
        }
        catch (OperationCanceledException)when (!ct.IsCancellationRequested)
        {
            return new(null, "El servidor tardó en responder. Comprueba la conexión e inténtalo nuevamente.");
        }
        catch (HttpRequestException)
        {
            return new(null, "No se pudo conectar de forma segura con el servidor. Comprueba la red y su dirección.");
        }
        catch (JsonException)
        {
            return new(null, "La respuesta del servidor no se pudo interpretar. Consulta al administrador.");
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(memoria);
        }
    }

    public async Task<ResultadoCierreSesion> CerrarAsync(Uri servidor, CancellationToken ct)
    {
        const string aviso = "El acceso se cerró en esta estación, pero no se pudo confirmar el cierre en el servidor.";
        if (DireccionServidor.Validar(servidor.AbsoluteUri).Error is not null)
            return new(false, aviso);
        using var espera = CancellationTokenSource.CreateLinkedTokenSource(ct);
        espera.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var solicitud = new HttpRequestMessage(HttpMethod.Delete, new Uri(servidor, "api/sesiones"));
            using var respuesta = await cliente.SendAsync(solicitud, HttpCompletionOption.ResponseHeadersRead, espera.Token);
            ct.ThrowIfCancellationRequested();
            return respuesta.StatusCode == HttpStatusCode.NoContent ? new(true, null) : new(false, aviso);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(false, aviso);
        }
        catch (HttpRequestException)
        {
            return new(false, aviso);
        }
        catch (ObjectDisposedException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
    }

    public void Dispose() => cliente.Dispose();
}
