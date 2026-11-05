using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Services;
public interface IClienteLecturas
{
    Task<ResultadoRemoto<T>> ConsultarAsync<T>(string ruta, CancellationToken ct);
}

public sealed class ClienteLecturas(HttpClient transporte, Uri servidor) : IClienteLecturas
{
    public async Task<ResultadoRemoto<T>> ConsultarAsync<T>(string ruta, CancellationToken ct)
    {
        if (!ruta.StartsWith("api/", StringComparison.Ordinal) || ruta.Contains('\\') || !Uri.TryCreate(servidor, ruta, out var destino) || destino.GetLeftPart(UriPartial.Authority) != servidor.GetLeftPart(UriPartial.Authority))
            throw new ArgumentException("Ruta de consulta no válida.", nameof(ruta));
        try
        {
            using var tiempo = CancellationTokenSource.CreateLinkedTokenSource(ct);
            tiempo.CancelAfter(TimeSpan.FromSeconds(15));
            using var respuesta = await transporte.GetAsync(destino, HttpCompletionOption.ResponseHeadersRead, tiempo.Token);
            await respuesta.Content.LoadIntoBufferAsync(1024 * 1024, tiempo.Token);
            if (!respuesta.IsSuccessStatusCode)
                return new(default, (int)respuesta.StatusCode switch
                {
                    401 => "Tu sesión terminó. Inicia sesión nuevamente.",
                    403 => "No tienes permiso para consultar estos datos.",
                    _ => "La consulta no pudo completarse. Comprueba la conexión con el servidor."
                });
            var datos = await respuesta.Content.ReadFromJsonAsync<T>(tiempo.Token);
            return new(datos, datos is null ? "El servidor no devolvió los datos solicitados." : null);
        }
        catch (OperationCanceledException)when (!ct.IsCancellationRequested)
        {
            return new(default, "El servidor tardó demasiado en responder.");
        }
        catch (HttpRequestException)
        {
            return new(default, "No hay conexión con el servidor. Puedes volver a consultar.");
        }
        catch (JsonException)
        {
            return new(default, "Los datos recibidos no se pudieron interpretar.");
        }
    }
}
