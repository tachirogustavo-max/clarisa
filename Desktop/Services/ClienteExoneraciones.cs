using System.Net.Http;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Services;
public interface IClienteExoneraciones
{
    Task<ResultadoRemoto<NotaConfirmada>> CambiarAsync(ContextoPlanilla contexto, Guid inscripcion, long version, string motivo, bool revocar, CancellationToken ct);
}
public sealed class ClienteExoneraciones(ClienteEvaluacion escritura) : IClienteExoneraciones
{
    public Task<ResultadoRemoto<NotaConfirmada>> CambiarAsync(ContextoPlanilla contexto, Guid inscripcion, long version, string motivo, bool revocar, CancellationToken ct) =>
        escritura.EnviarAsync<NotaConfirmada>(HttpMethod.Post, "api/calificaciones/exoneraciones" + (revocar ? "/revocar" : ""),
            new { contexto.SeccionId, contexto.MateriaId, contexto.LapsoId, InscripcionId = inscripcion, VersionEsperada = version, Motivo = motivo }, ct);
}
