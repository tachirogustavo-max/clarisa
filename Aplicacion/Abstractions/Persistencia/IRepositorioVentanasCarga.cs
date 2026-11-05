using Clarisa.Application.Common.Auditoria;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Abstractions.Persistencia;

// En una unidad de Escritura, la lectura de la ventana debe impedir que otra transacción la actualice antes de confirmar.
public interface IRepositorioVentanasCarga
{
    Task<VentanaCarga?> ObtenerPorLapsoAsync(Guid lapsoId, CancellationToken ct);
    Task<IReadOnlyList<VentanaCarga>> ListarPorLapsosAsync(IReadOnlyCollection<Guid> lapsoIds, CancellationToken ct);
    Task AgregarAsync(VentanaCarga ventana, CancellationToken ct);
    Task<long> ActualizarAsync(VentanaCarga ventana, CancellationToken ct);
    Task RegistrarMovimientoAsync(MovimientoVentanaCarga movimiento, CancellationToken ct);
    Task<IReadOnlyList<MovimientoVentanaCarga>> ListarMovimientosAsync(Guid lapsoId, int maximo, CancellationToken ct);
}
