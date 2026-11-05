using Clarisa.Application.Abstractions.Persistencia;

namespace Clarisa.Application.Abstractions.Seguridad;

// Unidad separada de IUnidadTrabajo para que los casos académicos no vean cuentas ni huellas de claves. Debe usar la misma base
// y el mismo turno de escritura que las unidades académicas: la cuenta y el docente vinculado se comprueban en una transacción.
public interface IFabricaUnidadSeguridad
{
    Task<IUnidadSeguridad> IniciarAsync(ModoUnidadTrabajo modo, CancellationToken ct);
}

public interface IUnidadSeguridad : IAsyncDisposable
{
    IRepositorioCuentas Cuentas { get; }
    IRepositorioSesiones Sesiones { get; }
    IRegistroAuditoriaCuentas Auditoria { get; }
    IRepositorioDocentes Docentes { get; }

    Task ConfirmarAsync(CancellationToken ct);
}
