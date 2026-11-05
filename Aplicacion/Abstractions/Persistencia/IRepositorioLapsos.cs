using Clarisa.Domain.Entities;

namespace Clarisa.Application.Abstractions.Persistencia;

public interface IRepositorioLapsos
{
    Task<Lapso?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Lapso>> ListarPorAnioAsync(Guid anioLectivoId, CancellationToken ct);
    Task<bool> ExisteOrdenAsync(Guid anioLectivoId, int orden, Guid? excluirId, CancellationToken ct);
    Task<bool> ExisteNombreAsync(Guid anioLectivoId, string nombre, Guid? excluirId, CancellationToken ct);
    Task AgregarAsync(Lapso lapso, CancellationToken ct);
    Task<long> ActualizarAsync(Lapso lapso, CancellationToken ct);
}
