using Clarisa.Domain.Entities;

namespace Clarisa.Application.Abstractions.Persistencia;

public interface IRepositorioAsignacionesDocentes
{
    Task<AsignacionDocente?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<bool> ExisteAsync(Guid seccionId, Guid materiaId, Guid docenteId, CancellationToken ct);
    Task<bool> DocenteTieneSeccionAsync(Guid docenteId, Guid seccionId, CancellationToken ct);
    Task<bool> ExistenEnSeccionAsync(Guid seccionId, CancellationToken ct);
    Task<IReadOnlyList<AsignacionDocente>> ListarPorSeccionAsync(Guid seccionId, CancellationToken ct);
    Task<IReadOnlyList<AsignacionDocente>> ListarPorDocenteAsync(Guid docenteId, CancellationToken ct);
    Task AgregarAsync(AsignacionDocente asignacion, CancellationToken ct);
    Task<long> ActualizarAsync(AsignacionDocente asignacion, CancellationToken ct);
    Task EliminarAsync(Guid id, long versionEsperada, CancellationToken ct);
}
