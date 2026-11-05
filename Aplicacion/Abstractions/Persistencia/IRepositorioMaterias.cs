using Clarisa.Application.Common.Consultas;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Abstractions.Persistencia;

public interface IRepositorioMaterias
{
    Task<Materia?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Materia>> ObtenerVariasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<bool> ExisteCodigoAsync(string codigo, Guid? excluirId, CancellationToken ct);
    Task<Pagina<Materia>> BuscarAsync(FiltroTexto filtro, ConsultaPagina consulta, CancellationToken ct);
    Task AgregarAsync(Materia materia, CancellationToken ct);
    Task<long> ActualizarAsync(Materia materia, CancellationToken ct);
}
