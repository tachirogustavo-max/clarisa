using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Clarisa.Desktop.Services;
public sealed class ClienteVentanas(HttpClient transporte, Uri servidor) : IClienteVentanas
{
    public async Task<RespuestaVentana> ConsultarAsync(Guid lapsoId, CancellationToken ct)
    {
        if (DireccionServidor.Validar(servidor.AbsoluteUri).Error is { } error)
            return new(null, error);
        try
        {
            using var espera = CancellationTokenSource.CreateLinkedTokenSource(ct);
            espera.CancelAfter(TimeSpan.FromSeconds(15));
            using var respuesta = await transporte.GetAsync(new Uri(servidor, "api/ventanas-carga/" + lapsoId), espera.Token);
            if (!respuesta.IsSuccessStatusCode)
                return new(null, "No se pudo consultar el estado de la carga.");
            await respuesta.Content.LoadIntoBufferAsync(64 * 1024, ct);
            return new(await respuesta.Content.ReadFromJsonAsync<EstadoVentanaRemota>(ct), null);
        }
        catch (OperationCanceledException)when (!ct.IsCancellationRequested)
        {
            return new(null, "El servidor no respondió a tiempo.");
        }
        catch (HttpRequestException)
        {
            return new(null, "No se pudo conectar con el servidor.");
        }
        catch (JsonException)
        {
            return new(null, "La respuesta no tiene un estado válido.");
        }
    }

    public async Task<RespuestaVentana> CambiarAsync(Guid lapsoId, bool abrir, long versionEsperada, CancellationToken ct)
    {
        var destino = DireccionServidor.Validar(servidor.AbsoluteUri);
        if (destino.Error is not null)
            return new(null, destino.Error);
        try
        {
            using var espera = CancellationTokenSource.CreateLinkedTokenSource(ct);
            espera.CancelAfter(TimeSpan.FromSeconds(15));
            using var respuesta = abrir
                ? await transporte.PostAsJsonAsync(new Uri(servidor, "api/ventanas-carga/abrir"), new { lapsoId, versionEsperada, hasta = (DateTimeOffset?)null }, espera.Token)
                : await transporte.PostAsJsonAsync(new Uri(servidor, "api/ventanas-carga/cerrar"), new { lapsoId, versionEsperada }, espera.Token);
            if (!respuesta.IsSuccessStatusCode)
                return new(null, (await Catalogos.RechazoCatalogo.LeerAsync<EstadoVentanaRemota>(respuesta, ct)).Error);
            await respuesta.Content.LoadIntoBufferAsync(64 * 1024, ct);
            var estado = await respuesta.Content.ReadFromJsonAsync<EstadoVentanaRemota>(ct);
            return new(estado, estado is null ? "No llegó una confirmación válida." : null);
        }
        catch (OperationCanceledException)when (!ct.IsCancellationRequested)
        {
            return new(null, "El servidor tardó en responder. El estado queda pendiente de comprobar.");
        }
        catch (HttpRequestException)
        {
            return new(null, "Se perdió la conexión. El estado queda pendiente de comprobar.");
        }
        catch (JsonException)
        {
            return new(null, "La confirmación no se pudo interpretar. Comprueba el estado de la carga.");
        }
    }
}
