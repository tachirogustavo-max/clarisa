using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Services;
public interface IClienteMateriaEvaluada
{
    Task<ResultadoRemoto<RegistroConfirmado>> CrearAsync(object solicitud, CancellationToken ct);
}
public sealed class ClienteMateriaEvaluada(HttpClient transporte, Uri servidor) : IClienteMateriaEvaluada
{
    public async Task<ResultadoRemoto<RegistroConfirmado>> CrearAsync(object datos, CancellationToken ct)
    {
        using var espera = CancellationTokenSource.CreateLinkedTokenSource(ct);
        espera.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using var solicitud = new HttpRequestMessage(HttpMethod.Post, new Uri(servidor, "api/materias/evaluadas")) { Content = JsonContent.Create(datos) };
            using var respuesta = await transporte.SendAsync(solicitud, HttpCompletionOption.ResponseHeadersRead, espera.Token);
            await respuesta.Content.LoadIntoBufferAsync(64 * 1024, espera.Token);
            if (!respuesta.IsSuccessStatusCode) return await RechazoCatalogo.LeerAsync<RegistroConfirmado>(respuesta, espera.Token);
            var registro = await respuesta.Content.ReadFromJsonAsync<RegistroConfirmado>(espera.Token);
            return new(registro, registro is null ? "No llegó una confirmación válida. La edición se conserva." : null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(null, "No se confirmó la materia ni su evaluación. Consulta antes de volver a guardar."); }
        catch (Exception error) when (error is HttpRequestException or JsonException) { return new(null, "No se confirmó la materia ni su evaluación. La edición se conserva."); }
    }
}
