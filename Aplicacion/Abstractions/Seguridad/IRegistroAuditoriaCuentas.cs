namespace Clarisa.Application.Abstractions.Seguridad;

public enum AccionAuditoriaCuenta
{
    Creacion = 1,
    Modificacion = 2,
    CambioClave = 3,
    Desactivacion = 4,
    InicioSesion = 5,
    CierreSesion = 6,
    PreparacionInicial = 7
}

// Nunca contiene claves, huellas ni tokens.
public sealed record EventoAuditoriaCuenta(Guid ActorUsuarioId, AccionAuditoriaCuenta Accion, Guid CuentaId, DateTimeOffset Ocurrido);

public interface IRegistroAuditoriaCuentas
{
    Task RegistrarAsync(EventoAuditoriaCuenta evento, CancellationToken ct);
}
