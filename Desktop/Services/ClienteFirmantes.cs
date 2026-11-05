using System.Net.Http;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Services;

public sealed record NombresFirmantesRemotos(string? Director, string? CoordinadorEvaluacion, string? CoordinadorEstudios);

public sealed record FirmantesRemotos(NombresFirmantesRemotos Nombres, int Numero, string? Motivo, DateTimeOffset? RegistradoEn);

public interface IClienteFirmantes
{
    Task<ResultadoRemoto<FirmantesRemotos>> ConsultarAsync(CancellationToken ct);
    Task<ResultadoRemoto<FirmantesRemotos>> CambiarAsync(Guid id, int numeroEsperado, NombresFirmantesRemotos nombres, string motivo,
        CancellationToken ct);
}

public sealed class ClienteFirmantes(IClienteLecturas lecturas, ClienteEvaluacion escritura) : IClienteFirmantes
{
    public async Task<ResultadoRemoto<FirmantesRemotos>> ConsultarAsync(CancellationToken ct)
    {
        var respuesta = await lecturas.ConsultarAsync<FirmantesRemotos>("api/firmantes-boletin", ct);
        return respuesta.Datos is { } datos && (datos.Nombres is null || datos.Numero < 0)
            ? new(null, "Los firmantes llegaron incompletos. No se mostraron.")
            : respuesta;
    }

    public Task<ResultadoRemoto<FirmantesRemotos>> CambiarAsync(Guid id, int numeroEsperado, NombresFirmantesRemotos nombres, string motivo,
        CancellationToken ct) =>
        escritura.EnviarAsync<FirmantesRemotos>(HttpMethod.Post, "api/firmantes-boletin", new
        {
            Id = id,
            NumeroEsperado = numeroEsperado,
            nombres.Director,
            nombres.CoordinadorEvaluacion,
            nombres.CoordinadorEstudios,
            Motivo = motivo
        }, ct);
}
