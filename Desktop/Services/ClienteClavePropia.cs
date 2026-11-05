using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Services;
public sealed record ResultadoClavePropia(bool Actualizada, bool Salir, string Mensaje, string? Campo = null);
public interface IClienteClavePropia
{
    Task<ResultadoClavePropia> CambiarAsync(SecureString actual, SecureString nueva, CancellationToken ct);
}
public sealed class ClienteClavePropia(HttpClient transporte, Uri servidor) : IClienteClavePropia
{
    public async Task<ResultadoClavePropia> CambiarAsync(SecureString actual, SecureString nueva, CancellationToken ct)
    {
        var memoriaActual = IntPtr.Zero;
        var memoriaNueva = IntPtr.Zero;
        using var espera = CancellationTokenSource.CreateLinkedTokenSource(ct);
        espera.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            memoriaActual = Marshal.SecureStringToGlobalAllocUnicode(actual);
            memoriaNueva = Marshal.SecureStringToGlobalAllocUnicode(nueva);
            using var solicitud = new HttpRequestMessage(HttpMethod.Post, new Uri(servidor, "api/cuenta/clave"))
            { Content = JsonContent.Create(new { ClaveActual = Marshal.PtrToStringUni(memoriaActual), ClaveNueva = Marshal.PtrToStringUni(memoriaNueva) }) };
            using var respuesta = await transporte.SendAsync(solicitud, HttpCompletionOption.ResponseHeadersRead, espera.Token);
            if (respuesta.StatusCode == HttpStatusCode.NoContent)
                return new(true, true, "Contraseña actualizada. Inicia sesión con la nueva contraseña.");
            if ((int)respuesta.StatusCode is 400 or 403 or 409 or 422 or 429)
            {
                await respuesta.Content.LoadIntoBufferAsync(64 * 1024, espera.Token);
                var rechazo = await RechazoCatalogo.LeerAsync<object>(respuesta, espera.Token);
                return new(false, false, rechazo.Error ?? "No se aceptó el cambio de contraseña.", rechazo.Campo);
            }
            if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
                return new(false, true, "Tu sesión terminó. Inicia sesión nuevamente.");
            return SinConfirmacion();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return SinConfirmacion(); }
        catch (Exception error) when (error is HttpRequestException or JsonException) { return SinConfirmacion(); }
        finally
        {
            if (memoriaActual != IntPtr.Zero) Marshal.ZeroFreeGlobalAllocUnicode(memoriaActual);
            if (memoriaNueva != IntPtr.Zero) Marshal.ZeroFreeGlobalAllocUnicode(memoriaNueva);
        }
    }
    private static ResultadoClavePropia SinConfirmacion() => new(false, true,
        "No se confirmó el cambio. Por seguridad, inicia sesión de nuevo: prueba primero la contraseña nueva. No se reenviará la solicitud.");
}
