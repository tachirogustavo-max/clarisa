using System.Net.Http;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Services;
public interface IClienteDocumentos
{
    Task<ResultadoRemoto<byte[]>> BoletinAsync(Guid inscripcionId, Guid? lapsoId, CancellationToken ct);
    Task<ResultadoRemoto<byte[]>> BoletinesSeccionAsync(Guid seccionId, Guid? lapsoId, CancellationToken ct);
}

public sealed class ClienteDocumentos(HttpClient transporte, Uri servidor) : IClienteDocumentos
{
    // Un boletín pesa poco; el de una sección completa lleva una página por estudiante.
    public const int MaximoBytesSeccion = 40 * 1024 * 1024;

    public Task<ResultadoRemoto<byte[]>> BoletinAsync(Guid inscripcionId, Guid? lapsoId, CancellationToken ct) =>
        DescargarAsync("api/boletines/" + inscripcionId + "/pdf" + Lapso(lapsoId), 10 * 1024 * 1024, TimeSpan.FromSeconds(30),
            "La inscripción ya no está disponible. Vuelve a consultar al estudiante.", ct);

    public Task<ResultadoRemoto<byte[]>> BoletinesSeccionAsync(Guid seccionId, Guid? lapsoId, CancellationToken ct) =>
        DescargarAsync("api/secciones/" + seccionId + "/boletines/pdf" + Lapso(lapsoId), MaximoBytesSeccion, TimeSpan.FromMinutes(3),
            "La sección ya no está disponible. Vuelve a consultarla.", ct);

    private static string Lapso(Guid? lapsoId) => lapsoId is null ? "" : "?lapsoId=" + lapsoId;

    private async Task<ResultadoRemoto<byte[]>> DescargarAsync(string ruta, int maximo, TimeSpan limite, string noEncontrado, CancellationToken ct)
    {
        try
        {
            using var tiempo = CancellationTokenSource.CreateLinkedTokenSource(ct);
            tiempo.CancelAfter(limite);
            using var respuesta = await transporte.GetAsync(new Uri(servidor, ruta), HttpCompletionOption.ResponseHeadersRead, tiempo.Token);
            // Un rechazo con motivo (por ejemplo, sección sin estudiantes activos) se muestra tal cual lo explica el servidor.
            if ((int)respuesta.StatusCode is 400 or 409 or 422)
                return new(null, (await Catalogos.RechazoCatalogo.LeerAsync<byte[]>(respuesta, tiempo.Token)).Error);
            if (!respuesta.IsSuccessStatusCode)
                return new(null, (int)respuesta.StatusCode switch
                {
                    401 => "Tu sesión terminó. Inicia sesión nuevamente antes de descargar el boletín.",
                    403 => "Tu cuenta no tiene permiso para descargar este boletín.",
                    404 => noEncontrado,
                    _ => "No se pudo generar el boletín. Vuelve a consultar sus datos antes de descargarlo."
                });
            if (respuesta.Content.Headers.ContentType?.MediaType != "application/pdf")
                return new(null, "El servidor no devolvió un documento PDF.");
            await respuesta.Content.LoadIntoBufferAsync(maximo, tiempo.Token);
            var contenido = await respuesta.Content.ReadAsByteArrayAsync(tiempo.Token);
            return contenido.Length >= 20 && contenido.AsSpan(0, 5).SequenceEqual("%PDF-"u8) ? new(contenido, null) : new(null, "El documento recibido está incompleto.");
        }
        catch (HttpRequestException)
        {
            return new(null, "No se pudo descargar el PDF. No se guardó un archivo incompleto.");
        }
        catch (OperationCanceledException)when (!ct.IsCancellationRequested)
        {
            return new(null, "El documento tardó demasiado en llegar. Puedes volver a solicitarlo.");
        }
    }
}
