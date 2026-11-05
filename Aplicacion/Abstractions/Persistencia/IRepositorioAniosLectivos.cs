using Clarisa.Application.Common.Consultas;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Abstractions.Persistencia;

public interface IRepositorioAniosLectivos
{
    Task<AnioLectivo?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken ct);
    Task<Pagina<AnioLectivo>> ListarAsync(ConsultaPagina consulta, CancellationToken ct);
    Task AgregarAsync(AnioLectivo anio, CancellationToken ct);
    Task<long> ActualizarAsync(AnioLectivo anio, CancellationToken ct);
}
