using Clarisa.Application.Common.Consultas;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Abstractions.Persistencia;

public interface IRepositorioEstudiantes
{
    Task<Estudiante?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Estudiante>> ObtenerVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<bool> ExisteIdentificacionAsync(string identificacion, Guid? excluirId, CancellationToken ct);
    Task<Pagina<Estudiante>> BuscarAsync(FiltroTexto filtro, ConsultaPagina consulta, CancellationToken ct);
    Task AgregarAsync(Estudiante estudiante, CancellationToken ct);
    Task<long> ActualizarAsync(Estudiante estudiante, CancellationToken ct);
}
