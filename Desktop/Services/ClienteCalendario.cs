using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Services;
public sealed record ConfiguracionCargaRemota(Guid AnioLectivoId, long VersionAnio, IReadOnlyList<LapsoCargaRemoto> Lapsos)
{
    public bool? PoliticaActiva { get; init; }
}
public interface IClienteCalendario
{
    Task<ResultadoRemoto<RegistroConfirmado>> SeleccionarActualAsync(Guid anioId, long version, Guid lapsoId, CancellationToken ct);
}

public sealed class ClienteCalendario(HttpClient transporte, Uri servidor) : IClienteCalendario
{
    public async Task<ResultadoRemoto<RegistroConfirmado>> SeleccionarActualAsync(Guid anioId, long version, Guid lapsoId, CancellationToken ct)
    {
        try
        {
            using var espera = CancellationTokenSource.CreateLinkedTokenSource(ct);
            espera.CancelAfter(TimeSpan.FromSeconds(15));
            using var respuesta = await transporte.PutAsJsonAsync(new Uri(servidor, "api/anios-lectivos/" + anioId + "/lapso-actual"), new { AnioLectivoId = anioId, VersionEsperada = version, LapsoId = lapsoId }, espera.Token);
            await respuesta.Content.LoadIntoBufferAsync(64 * 1024, espera.Token);
            if ((int)respuesta.StatusCode is 400 or 409 or 422)
                return await Catalogos.RechazoCatalogo.LeerAsync<RegistroConfirmado>(respuesta, espera.Token);
            if (!respuesta.IsSuccessStatusCode)
                return new(null, "No se confirmó el cambio. Consulta el año escolar antes de volver a intentarlo.");
            var datos = await respuesta.Content.ReadFromJsonAsync<RegistroConfirmado>(ct);
            return new(datos, datos is null ? "No llegó una confirmación completa." : null);
        }
        catch (HttpRequestException)
        {
            return new(null, "Se perdió la conexión. Consulta el estado antes de repetir el cambio.");
        }
        catch (OperationCanceledException)when (!ct.IsCancellationRequested)
        {
            return new(null, "La respuesta tardó demasiado. Consulta el estado antes de repetir el cambio.");
        }
        catch (JsonException)
        {
            return new(null, "No se pudo interpretar la confirmación. Consulta el estado del año escolar.");
        }
    }
}
