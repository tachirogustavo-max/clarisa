using Clarisa.Domain.Entities;

namespace Clarisa.Application.Abstractions.Persistencia;

public interface IRepositorioFirmantes
{
    Task<FirmantesBoletin?> VigentesAsync(CancellationToken ct);
    Task<FirmantesBoletin?> ObtenerAsync(Guid id, CancellationToken ct);
    Task AgregarAsync(FirmantesBoletin firmantes, CancellationToken ct);
}
