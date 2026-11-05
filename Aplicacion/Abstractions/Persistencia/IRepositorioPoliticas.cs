using Clarisa.Domain.Entities;

namespace Clarisa.Application.Abstractions.Persistencia;

public interface IRepositorioPoliticas
{
    Task<PoliticaAcademica?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<PoliticaAcademica?> VigenteAsync(Guid anioLectivoId, CancellationToken ct);
    Task<IReadOnlyList<PoliticaAcademica>> ListarAsync(Guid anioLectivoId, CancellationToken ct);
    Task<int> SiguienteNumeroAsync(Guid anioLectivoId, CancellationToken ct);
    Task AgregarAsync(PoliticaAcademica politica, CancellationToken ct);
    Task<long> ActualizarAsync(PoliticaAcademica politica, CancellationToken ct);
}
