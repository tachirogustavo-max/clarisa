using Clarisa.Application.Common.Consultas;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Abstractions.Seguridad;

public interface IRepositorioCuentas
{
    Task<CuentaUsuario?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<CuentaUsuario?> ObtenerPorUsuarioAsync(string usuarioNormalizado, CancellationToken ct);
    Task<bool> ExisteUsuarioAsync(string usuarioNormalizado, Guid? excluirId, CancellationToken ct);
    Task<bool> ExisteDocenteVinculadoAsync(Guid docenteId, Guid? excluirId, CancellationToken ct);
    Task<int> ContarAdministradoresActivosAsync(CancellationToken ct);
    Task<bool> ExisteAlgunaAsync(CancellationToken ct);
    Task<Pagina<CuentaUsuario>> BuscarAsync(FiltroTexto filtro, ConsultaPagina consulta, CancellationToken ct);
    Task AgregarAsync(CuentaUsuario cuenta, CancellationToken ct);
    Task<long> ActualizarAsync(CuentaUsuario cuenta, CancellationToken ct);
}
