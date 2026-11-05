using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Clarisa.Desktop.Services.Catalogos;
public sealed class ClienteCatalogo<T> : IClienteCatalogo<T>
{
    private readonly HttpClient transporte;
    private readonly Uri destino;
    public ClienteCatalogo(HttpClient transporte, Uri servidor, string recurso)
    {
        if (DireccionServidor.Validar(servidor.AbsoluteUri).Error is not null)
            throw new ArgumentException("Servidor no válido.", nameof(servidor));
        if (recurso is not ("estudiantes" or "docentes" or "materias" or "grados" or "secciones" or "anios-lectivos" or "usuarios" or "respaldos" or "asignaciones" or "lapsos" or "inscripciones"))
            throw new ArgumentException("Catálogo no reconocido.", nameof(recurso));
        this.transporte = transporte;
        destino = new Uri(servidor, "api/" + recurso);
    }

    public Task<ResultadoRemoto<PaginaRemota<T>>> ConsultarAsync(ConsultaCatalogo consulta, CancellationToken ct)
    {
        if (consulta.Pagina < 1 || consulta.Tamanio is < 1 or > 200)
            throw new ArgumentOutOfRangeException(nameof(consulta));
        var direccion = destino + "?pagina=" + consulta.Pagina + "&tamanio=" + consulta.Tamanio + "&texto=" + Uri.EscapeDataString(consulta.Texto) + "&incluirInactivos=" + consulta.IncluirInactivos.ToString().ToLowerInvariant() + (consulta.AnioLectivoId is { } anio ? "&anioLectivoId=" + anio : "");
        return EnviarAsync<PaginaRemota<T>>(new HttpRequestMessage(HttpMethod.Get, direccion), ct);
    }

    public Task<ResultadoRemoto<RegistroConfirmado>> GuardarAsync<TCambio>(Guid id, bool nuevo, TCambio cambio, CancellationToken ct) => EnviarAsync<RegistroConfirmado>(new HttpRequestMessage(nuevo ? HttpMethod.Post : HttpMethod.Put, nuevo ? destino : new Uri(destino + "/" + id)) { Content = JsonContent.Create(cambio) }, ct);
    private async Task<ResultadoRemoto<TDato>> EnviarAsync<TDato>(HttpRequestMessage solicitud, CancellationToken ct)
    {
        using (solicitud)
            try
            {
                using var tiempo = CancellationTokenSource.CreateLinkedTokenSource(ct);
                tiempo.CancelAfter(TimeSpan.FromSeconds(15));
                using var respuesta = await transporte.SendAsync(solicitud, HttpCompletionOption.ResponseHeadersRead, tiempo.Token);
                await respuesta.Content.LoadIntoBufferAsync(1024 * 1024, tiempo.Token);
                if (!respuesta.IsSuccessStatusCode)
                    return await RechazoCatalogo.LeerAsync<TDato>(respuesta, tiempo.Token);
                var datos = await respuesta.Content.ReadFromJsonAsync<TDato>(tiempo.Token);
                return new(datos, datos is null ? "La respuesta llegó incompleta." : null);
            }
            catch (OperationCanceledException)when (!ct.IsCancellationRequested)
            {
                return new(default, "Se agotó el tiempo de espera. No se pudo confirmar la operación.");
            }
            catch (HttpRequestException)
            {
                return new(default, "No se pudo confirmar la operación. Comprueba la conexión con el servidor.");
            }
            catch (JsonException)
            {
                return new(default, "No se pudo interpretar la respuesta del servidor.");
            }
    }
}
