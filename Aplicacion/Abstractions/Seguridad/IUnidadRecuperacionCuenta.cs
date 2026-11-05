using Clarisa.Application.Abstractions.Persistencia;

namespace Clarisa.Application.Abstractions.Seguridad;

public sealed record OperadorRecuperacion(string IdentidadWindows, string UsuarioBase);
public sealed record EventoRecuperacionCuenta(Guid Id, Guid CuentaId, long VersionResultante,
    OperadorRecuperacion Operador, string Motivo, DateTimeOffset Ocurrido);

public interface IUnidadRecuperacionCuenta : IUnidadSeguridad
{
    Task RegistrarRecuperacionAsync(EventoRecuperacionCuenta evento, CancellationToken ct);
}

// Composición exclusiva del mantenimiento local. No se registra en la API.
public interface IFabricaRecuperacionCuenta
{
    Task<IUnidadRecuperacionCuenta> IniciarAsync(ModoUnidadTrabajo modo, CancellationToken ct);
}
