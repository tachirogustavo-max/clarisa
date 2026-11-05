using Clarisa.Application.Common.Consultas;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Abstractions.Persistencia;

public interface IRepositorioSecciones
{
    Task<Seccion?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<bool> ExisteGradoAsync(Guid anioLectivoId, string grado, CancellationToken ct);
    Task<IReadOnlyList<string>> GradosAsync(Guid anioLectivoId, CancellationToken ct);
    Task<bool> ExisteNombreAsync(Guid anioLectivoId, string grado, string nombre, Guid? excluirId, CancellationToken ct);
    Task<Pagina<Seccion>> ListarPorAnioAsync(Guid anioLectivoId, ConsultaPagina consulta, CancellationToken ct);
    Task AgregarAsync(Seccion seccion, CancellationToken ct);
    Task<long> ActualizarAsync(Seccion seccion, CancellationToken ct);
    Task EliminarAsync(Guid id, long versionEsperada, CancellationToken ct);
}
