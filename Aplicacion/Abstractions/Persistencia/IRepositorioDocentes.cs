using Clarisa.Application.Common.Consultas;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Abstractions.Persistencia;

public interface IRepositorioDocentes
{
    Task<Docente?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<bool> ExisteIdentificacionAsync(string identificacion, Guid? excluirId, CancellationToken ct);
    Task<Pagina<Docente>> BuscarAsync(FiltroTexto filtro, ConsultaPagina consulta, CancellationToken ct);
    Task AgregarAsync(Docente docente, CancellationToken ct);
    Task<long> ActualizarAsync(Docente docente, CancellationToken ct);
}
