using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Infrastructure.Persistence.Sesion;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

// El evento se guarda en UTC; el desplazamiento del reloj del servidor no forma parte de la trazabilidad.
internal sealed class RegistroAuditoriaMariaDb(SesionMariaDb sesion) : IRegistroAuditoria
{
    public Task RegistrarAsync(EventoAuditoria evento, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return sesion.EjecutarAsync("""
            INSERT INTO auditoria (actor_usuario_id, accion, entidad, entidad_id, ocurrido_utc_ticks)
            VALUES (@actor, @accion, @entidad, @entidad_id, @ocurrido)
            """, ct,
            Parametros.Id("@actor", evento.ActorUsuarioId),
            Parametros.Entero("@accion", (int)evento.Accion),
            Parametros.Texto("@entidad", evento.Entidad),
            Parametros.Id("@entidad_id", evento.EntidadId),
            Parametros.Instante("@ocurrido", evento.Ocurrido));
    }
}
