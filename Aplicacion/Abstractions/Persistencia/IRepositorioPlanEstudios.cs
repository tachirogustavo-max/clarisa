using Clarisa.Domain.Entities;

namespace Clarisa.Application.Abstractions.Persistencia;

public interface IRepositorioPlanEstudios
{
    Task<PlanMateria?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<PlanMateria?> BuscarAsync(Guid anioLectivoId, string grado, Guid materiaId, CancellationToken ct);
    Task<IReadOnlyList<PlanMateria>> ListarAsync(Guid anioLectivoId, string? grado, int maximo, CancellationToken ct);
    Task<long> ContarAsync(Guid anioLectivoId, CancellationToken ct);
    Task AgregarAsync(PlanMateria plan, CancellationToken ct);
    Task<long> ActualizarAsync(PlanMateria plan, CancellationToken ct);
    Task EliminarAsync(Guid id, long versionEsperada, CancellationToken ct);
}
