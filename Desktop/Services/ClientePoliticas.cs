using System.Net.Http;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Services;

public sealed record ReglasRemotas(int? DecimalesCaptura, int? DecimalesResultado, decimal? NotaAprobatoria, int? Formula, int? Redondeo,
    int? Exonerado, decimal? PesoLapso1, decimal? PesoLapso2, decimal? PesoLapso3);

public sealed record PoliticaRemota(Guid Id, Guid AnioLectivoId, int Numero, int Estado, ReglasRemotas Reglas, string Motivo,
    IReadOnlyList<string> Faltantes, DateTimeOffset CreadaEn, DateTimeOffset? ActivadaEn, string? MotivoActivacion, long Version)
{
    public const int Borrador = 1;
    public const int Vigente = 2;
    public string EstadoTexto => Estado switch { Borrador => "Borrador", Vigente => "Vigente", _ => "Reemplazada" };
    public string Resumen => Faltantes.Count == 0 ? "Completa" : "Falta: " + string.Join(", ", Faltantes);
}

public sealed record CierreSeccionRemoto(Guid Id, Guid AnioLectivoId, Guid SeccionId, int Numero, Guid PoliticaId, int Estado,
    DateTimeOffset CerradoEn, DateTimeOffset? AnuladoEn, string? MotivoAnulacion, long Version)
{
    public const int Vigente = 1;
    public string EstadoTexto => Estado == Vigente ? "Vigente" : "Anulado";
    public string Fecha => CerradoEn.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}

public interface IClientePoliticas
{
    Task<ResultadoRemoto<IReadOnlyList<PoliticaRemota>>> ListarAsync(Guid anioId, CancellationToken ct);
    Task<ResultadoRemoto<PoliticaRemota>> CrearAsync(Guid id, Guid anioId, Guid? basadaEn, string motivo, CancellationToken ct);
    Task<ResultadoRemoto<PoliticaRemota>> EditarAsync(Guid id, long version, string motivo, ReglasRemotas reglas, CancellationToken ct);
    Task<ResultadoRemoto<PoliticaRemota>> ActivarAsync(Guid id, long version, string motivo, CancellationToken ct);
    Task<ResultadoRemoto<IReadOnlyList<CierreSeccionRemoto>>> CierresAsync(Guid seccionId, CancellationToken ct);
    Task<ResultadoRemoto<CierreSeccionRemoto>> CerrarAsync(Guid id, Guid seccionId, CancellationToken ct);
    Task<ResultadoRemoto<CierreSeccionRemoto>> AnularAsync(Guid id, long version, string motivo, CancellationToken ct);
}

// Lecturas con el cliente común y escrituras con el de evaluación: mismos límites de tiempo, tamaño y tratamiento de rechazos.
public sealed class ClientePoliticas(IClienteLecturas lecturas, ClienteEvaluacion escritura) : IClientePoliticas
{
    public async Task<ResultadoRemoto<IReadOnlyList<PoliticaRemota>>> ListarAsync(Guid anioId, CancellationToken ct)
    {
        var respuesta = await lecturas.ConsultarAsync<IReadOnlyList<PoliticaRemota>>($"api/anios-lectivos/{anioId}/politicas", ct);
        return respuesta.Datos is { } datos && (datos.Count > 200 || datos.Any(p => p is null || p.AnioLectivoId != anioId || p.Reglas is null
            || p.Faltantes is null || p.Estado is < 1 or > 3))
            ? new(null, "Las versiones de la política llegaron incompletas. No se mostraron.")
            : respuesta;
    }

    public Task<ResultadoRemoto<PoliticaRemota>> CrearAsync(Guid id, Guid anioId, Guid? basadaEn, string motivo, CancellationToken ct) =>
        escritura.EnviarAsync<PoliticaRemota>(HttpMethod.Post, "api/politicas-academicas",
            new { Id = id, AnioLectivoId = anioId, BasadaEn = basadaEn, Motivo = motivo, Reglas = (ReglasRemotas?)null }, ct);

    public Task<ResultadoRemoto<PoliticaRemota>> EditarAsync(Guid id, long version, string motivo, ReglasRemotas reglas, CancellationToken ct) =>
        escritura.EnviarAsync<PoliticaRemota>(HttpMethod.Put, $"api/politicas-academicas/{id}",
            new { Id = id, VersionEsperada = version, Motivo = motivo, Reglas = reglas }, ct);

    public Task<ResultadoRemoto<PoliticaRemota>> ActivarAsync(Guid id, long version, string motivo, CancellationToken ct) =>
        escritura.EnviarAsync<PoliticaRemota>(HttpMethod.Post, $"api/politicas-academicas/{id}/activar",
            new { Id = id, VersionEsperada = version, Motivo = motivo }, ct);

    public async Task<ResultadoRemoto<IReadOnlyList<CierreSeccionRemoto>>> CierresAsync(Guid seccionId, CancellationToken ct)
    {
        var respuesta = await lecturas.ConsultarAsync<IReadOnlyList<CierreSeccionRemoto>>($"api/secciones/{seccionId}/cierres", ct);
        return respuesta.Datos is { } datos && (datos.Count > 200 || datos.Any(c => c is null || c.SeccionId != seccionId))
            ? new(null, "Los cierres de la sección llegaron incompletos. No se mostraron.")
            : respuesta;
    }

    public Task<ResultadoRemoto<CierreSeccionRemoto>> CerrarAsync(Guid id, Guid seccionId, CancellationToken ct) =>
        escritura.EnviarAsync<CierreSeccionRemoto>(HttpMethod.Post, "api/cierres-resultados", new { Id = id, SeccionId = seccionId }, ct);

    public Task<ResultadoRemoto<CierreSeccionRemoto>> AnularAsync(Guid id, long version, string motivo, CancellationToken ct) =>
        escritura.EnviarAsync<CierreSeccionRemoto>(HttpMethod.Post, $"api/cierres-resultados/{id}/anular",
            new { Id = id, VersionEsperada = version, Motivo = motivo }, ct);
}
