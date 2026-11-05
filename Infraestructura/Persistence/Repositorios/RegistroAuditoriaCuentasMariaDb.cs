using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Infrastructure.Persistence.Sesion;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

internal sealed class RegistroAuditoriaCuentasMariaDb(SesionMariaDb sesion) : IRegistroAuditoriaCuentas
{
    public Task RegistrarAsync(EventoAuditoriaCuenta evento, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return sesion.EjecutarAsync("""
            INSERT INTO auditoria_cuentas (actor_usuario_id, accion, cuenta_id, ocurrido_utc_ticks)
            VALUES (@actor, @accion, @cuenta, @ocurrido)
            """, ct,
            Parametros.Id("@actor", evento.ActorUsuarioId),
            Parametros.Entero("@accion", (int)evento.Accion),
            Parametros.Id("@cuenta", evento.CuentaId),
            Parametros.Instante("@ocurrido", evento.Ocurrido));
    }
}
