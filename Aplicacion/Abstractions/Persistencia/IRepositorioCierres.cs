using Clarisa.Domain.Entities;

namespace Clarisa.Application.Abstractions.Persistencia;

public interface IRepositorioCierres
{
    Task<CierreResultados?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<CierreResultados?> VigenteAsync(Guid seccionId, CancellationToken ct);
    Task<IReadOnlyList<CierreResultados>> ListarAsync(Guid seccionId, CancellationToken ct);
    Task<int> SiguienteNumeroAsync(Guid seccionId, CancellationToken ct);
    Task<IReadOnlyList<ResultadoCerrado>> ResultadosAsync(Guid cierreId, IReadOnlyCollection<Guid> inscripcionIds, CancellationToken ct);
    // La copia y su cabecera se escriben juntas en la unidad del caso: un cierre nunca queda sin sus resultados.
    Task AgregarAsync(CierreResultados cierre, IReadOnlyList<ResultadoCerrado> resultados, CancellationToken ct);
    Task<long> ActualizarAsync(CierreResultados cierre, CancellationToken ct);
}
