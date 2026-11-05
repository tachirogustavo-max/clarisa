using System.Net.Http;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Services;

public interface IClientePdfReportes
{
    Task<ResultadoRemoto<byte[]>> DescargarAsync(FiltroReporteRemoto filtro, CancellationToken ct);
}

public sealed class ClientePdfReportes(HttpClient transporte, Uri servidor) : IClientePdfReportes
{
    public async Task<ResultadoRemoto<byte[]>> DescargarAsync(FiltroReporteRemoto filtro, CancellationToken ct)
    {
        try
        {
            using var tiempo = CancellationTokenSource.CreateLinkedTokenSource(ct);
            tiempo.CancelAfter(TimeSpan.FromSeconds(30));
            using var respuesta = await transporte.GetAsync(new Uri(servidor, RutasReportes.Crear(filtro, true)), HttpCompletionOption.ResponseHeadersRead, tiempo.Token);
            if (!respuesta.IsSuccessStatusCode)
            {
                await respuesta.Content.LoadIntoBufferAsync(64 * 1024, tiempo.Token);
                return await RechazoCatalogo.LeerAsync<byte[]>(respuesta, tiempo.Token);
            }
            if (respuesta.Content.Headers.ContentType?.MediaType != "application/pdf") return new(null, "El servidor no devolvió un PDF.");
            await respuesta.Content.LoadIntoBufferAsync(10 * 1024 * 1024, tiempo.Token);
            var contenido = await respuesta.Content.ReadAsByteArrayAsync(tiempo.Token);
            return contenido.Length >= 20 && contenido.AsSpan(0, 5).SequenceEqual("%PDF-"u8)
                ? new(contenido, null) : new(null, "El informe llegó incompleto. No se guardó ningún archivo.");
        }
        catch (HttpRequestException) { return new(null, "No se pudo descargar el informe. Los filtros se conservan."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(null, "El informe tardó demasiado. Reduce los filtros y vuelve a consultar."); }
    }
}
