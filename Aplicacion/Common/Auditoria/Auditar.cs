using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Seguridad;

namespace Clarisa.Application.Common.Auditoria;

internal static class Auditar
{
    public static Task RegistrarAsync(IUnidadTrabajo unidad, IReloj reloj, Actor actor, AccionAuditoria accion,
        string entidad, Guid entidadId, CancellationToken ct) =>
        unidad.Auditoria.RegistrarAsync(new EventoAuditoria(actor.UsuarioId, accion, entidad, entidadId, reloj.Ahora), ct);
}
