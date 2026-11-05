using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Services;
public sealed record CuentaRemota(Guid Id, string Usuario, string Nombre, string Perfil, Guid? DocenteId, bool Activo, long Version)
{
    public string PerfilVisible => Perfil == "Administracion" ? "Dirección" : Perfil;
}

public sealed record CambioCuenta(Guid Id, long VersionEsperada, string Usuario, string Nombre, string Perfil, Guid? DocenteId, bool Activo);
public interface IClienteCuentas
{
    Task<ResultadoRemoto<RegistroConfirmado>> GuardarAsync(CambioCuenta cambio, bool nuevo, SecureString clave, CancellationToken ct);
}

public sealed class ClienteCuentas(HttpClient transporte, Uri servidor) : IClienteCuentas
{
    public async Task<ResultadoRemoto<RegistroConfirmado>> GuardarAsync(CambioCuenta cambio, bool nuevo, SecureString clave, CancellationToken ct)
    {
        var memoria = Marshal.SecureStringToGlobalAllocUnicode(clave);
        try
        {
            var cuerpo = new
            {
                cambio.Id,
                cambio.VersionEsperada,
                cambio.Usuario,
                cambio.Nombre,
                cambio.Perfil,
                cambio.DocenteId,
                cambio.Activo,
                Clave = clave.Length == 0 ? null : Marshal.PtrToStringUni(memoria)
            };
            using var solicitud = new HttpRequestMessage(nuevo ? HttpMethod.Post : HttpMethod.Put, new Uri(servidor, "api/usuarios" + (nuevo ? "" : "/" + cambio.Id)))
            {
                Content = JsonContent.Create(cuerpo)
            };
            using var espera = CancellationTokenSource.CreateLinkedTokenSource(ct);
            espera.CancelAfter(TimeSpan.FromSeconds(15));
            using var respuesta = await transporte.SendAsync(solicitud, espera.Token);
            await respuesta.Content.LoadIntoBufferAsync(64 * 1024, ct);
            if (!respuesta.IsSuccessStatusCode)
                return await RechazoCatalogo.LeerAsync<RegistroConfirmado>(respuesta, ct);
            var datos = await respuesta.Content.ReadFromJsonAsync<RegistroConfirmado>(ct);
            return new(datos, datos is null ? "La confirmación de la cuenta llegó incompleta." : null);
        }
        catch (HttpRequestException)
        {
            return new(null, "No se pudo confirmar el cambio. La edición se conserva.");
        }
        catch (OperationCanceledException)when (!ct.IsCancellationRequested)
        {
            return new(null, "El servidor tardó en responder. No se confirmó el cambio.");
        }
        catch (JsonException)
        {
            return new(null, "No se pudo interpretar la confirmación de la cuenta.");
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(memoria);
        }
    }
}
