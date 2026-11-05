using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Services;
public sealed record RespaldoRemoto(Guid Id, string Nombre, DateTimeOffset CreadoEn, long Bytes, bool Verificado)
{
    public string Tamano => (Bytes / 1024m / 1024m).ToString("0.0") + " MB";
    public string Estado => Verificado ? "Integridad verificada" : "Por comprobar";
}

public sealed record OperacionRespaldo(Guid Id, string Tipo, string Estado, string Mensaje);
public sealed record ResultadoSolicitudRespaldo(OperacionRespaldo? Datos, string? Error, bool Rechazada = false);
public sealed record GuiaRestauracion(bool Disponible, string Mensaje, string[] Pasos);
public interface IClienteGuiaRestauracion
{
    Task<ResultadoRemoto<GuiaRestauracion>> ConsultarGuiaAsync(CancellationToken ct);
}
public interface IClienteRespaldos
{
    Task<ResultadoSolicitudRespaldo> IniciarAsync(Guid operacionId, CancellationToken ct);
    Task<ResultadoRemoto<OperacionRespaldo>> ConsultarAsync(Guid operacionId, CancellationToken ct);
}

public sealed class ClienteRespaldos(HttpClient transporte, Uri servidor, IClienteLecturas lecturas) : IClienteRespaldos, IClienteGuiaRestauracion
{
    public Task<ResultadoRemoto<GuiaRestauracion>> ConsultarGuiaAsync(CancellationToken ct) => lecturas.ConsultarAsync<GuiaRestauracion>("api/respaldos/restauracion", ct);
    public Task<ResultadoRemoto<OperacionRespaldo>> ConsultarAsync(Guid operacionId, CancellationToken ct) => lecturas.ConsultarAsync<OperacionRespaldo>("api/respaldos/operaciones/" + operacionId, ct);
    public async Task<ResultadoSolicitudRespaldo> IniciarAsync(Guid operacionId, CancellationToken ct)
    {
        using var espera = CancellationTokenSource.CreateLinkedTokenSource(ct);
        espera.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using var solicitud = new HttpRequestMessage(HttpMethod.Post, new Uri(servidor, "api/respaldos"))
            { Content = JsonContent.Create(new { OperacionId = operacionId }) };
            using var respuesta = await transporte.SendAsync(solicitud, HttpCompletionOption.ResponseHeadersRead, espera.Token);
            if ((int)respuesta.StatusCode is 400 or 409 or 422)
                return new(null, (await Catalogos.RechazoCatalogo.LeerAsync<OperacionRespaldo>(respuesta, espera.Token)).Error, true);
            if (!respuesta.IsSuccessStatusCode)
            {
                var rechazada = (int)respuesta.StatusCode is 400 or 401 or 403 or 404 or 405 or 409 or 413 or 415 or 422 or 429;
                return new(null, rechazada
                    ? "El servidor rechazó la solicitud. Revisa tu sesión, la preparación de la clave y si ya hay una copia en curso."
                    : "El servidor no confirmó la operación. Comprueba su estado antes de volver a solicitarla.", rechazada);
            }
            await respuesta.Content.LoadIntoBufferAsync(64 * 1024, espera.Token);
            var datos = await respuesta.Content.ReadFromJsonAsync<OperacionRespaldo>(espera.Token);
            return new(datos, datos is null ? "No llegó la confirmación de la operación." : null);
        }
        catch (HttpRequestException)
        {
            return new(null, "Se perdió la conexión. Comprueba el estado de la operación.");
        }
        catch (OperationCanceledException)when (!ct.IsCancellationRequested)
        {
            return new(null, "La respuesta se demoró. Comprueba el estado de la operación.");
        }
        catch (JsonException)
        {
            return new(null, "No se pudo interpretar la respuesta. Comprueba el estado de la operación.");
        }
    }
}
