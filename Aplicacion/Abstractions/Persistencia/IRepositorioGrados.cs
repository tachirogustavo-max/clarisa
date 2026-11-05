using Clarisa.Application.Common.Consultas;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Abstractions.Persistencia;

public interface IRepositorioGrados
{
    Task<Grado?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<Grado?> BuscarPorNombreAsync(string nombre, CancellationToken ct);
    Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken ct);
    Task<Pagina<Grado>> BuscarAsync(FiltroTexto filtro, ConsultaPagina consulta, CancellationToken ct);
    Task AgregarAsync(Grado grado, CancellationToken ct);
    Task<long> ActualizarAsync(Grado grado, CancellationToken ct);
    // Cambia el nombre guardado en las secciones y en el plan de estudios que usaban el nombre anterior.
    Task RenombrarEnUsoAsync(string anterior, string nuevo, CancellationToken ct);
}
