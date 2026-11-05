using Clarisa.Domain.Entities;

namespace Clarisa.Application.Abstractions.Persistencia;

public interface IRepositorioEscalaCualitativa
{
    Task<LetraCualitativa?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<LetraCualitativa>> ListarAsync(Guid anioLectivoId, CancellationToken ct);
    Task<bool> EnUsoAsync(Guid anioLectivoId, string letra, CancellationToken ct);
    Task AgregarAsync(LetraCualitativa letra, CancellationToken ct);
    Task EliminarAsync(Guid id, long versionEsperada, CancellationToken ct);
}
