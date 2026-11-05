using Clarisa.Domain.Entities;

namespace Clarisa.Application.Abstractions.Persistencia;

public interface IRepositorioInscripciones
{
    Task<Inscripcion?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Inscripcion>> ObtenerVariasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<bool> ExisteActivaEnSeccionAsync(Guid estudianteId, Guid seccionId, CancellationToken ct);
    Task<IReadOnlyList<Inscripcion>> ListarActivasAsync(Guid seccionId, CancellationToken ct);
    Task<bool> ExistenEnSeccionAsync(Guid seccionId, CancellationToken ct);
    Task AgregarAsync(Inscripcion inscripcion, CancellationToken ct);
    Task<long> ActualizarAsync(Inscripcion inscripcion, CancellationToken ct);
}
