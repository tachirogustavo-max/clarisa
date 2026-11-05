using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Calificaciones;

// Único camino de escritura de un resultado: fila versionada, historial con estado anterior y nuevo, y auditoría en la misma unidad.
internal static class RegistroResultados
{
    public static async Task<NotaGuardada> AplicarAsync(IUnidadTrabajo unidad, IReloj reloj, Actor actor, ContextoCalificacion contexto,
        Guid inscripcionId, Calificacion? actual, Action<Calificacion> cambiar, AccionAuditoria accion, string? motivo,
        DateTimeOffset ahora, CancellationToken ct)
    {
        var tipoAnterior = actual?.Tipo ?? TipoResultado.Pendiente;
        var valorAnterior = actual?.Valor;
        var letraAnterior = actual?.Letra;
        var inasistenciasAnteriores = actual?.Inasistencias;
        Calificacion calificacion;
        long version;
        if (actual is null)
        {
            calificacion = new Calificacion(Guid.CreateVersion7(ahora), inscripcionId, contexto.Materia.Id, contexto.Lapso.Id, valor: null,
                version: 1);
            Error.DesdeDominio(() => cambiar(calificacion));
            await unidad.Calificaciones.AgregarAsync(calificacion, ct);
            version = calificacion.Version;
        }
        else
        {
            calificacion = actual;
            Error.DesdeDominio(() => cambiar(calificacion));
            version = await unidad.Calificaciones.ActualizarAsync(calificacion, ct);
        }

        await unidad.Calificaciones.RegistrarCambioAsync(new CambioCalificacion(calificacion.Id, inscripcionId, contexto.Materia.Id,
            contexto.Lapso.Id, valorAnterior, calificacion.Valor, version, actor.UsuarioId, ahora)
        {
            TipoAnterior = tipoAnterior,
            TipoNuevo = calificacion.Tipo,
            LetraAnterior = letraAnterior,
            LetraNueva = calificacion.Letra,
            Motivo = motivo,
            InasistenciasAnteriores = inasistenciasAnteriores,
            InasistenciasNuevas = calificacion.Inasistencias
        }, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, accion, nameof(Calificacion), calificacion.Id, ct);
        return Guardada(inscripcionId, calificacion, version, cambio: true);
    }

    public static NotaGuardada Guardada(Guid inscripcionId, Calificacion? calificacion, long version, bool cambio) =>
        new(inscripcionId, calificacion?.Valor, version, cambio)
        {
            Tipo = calificacion?.Tipo ?? TipoResultado.Pendiente,
            Letra = calificacion?.Letra,
            Inasistencias = calificacion?.Inasistencias
        };
}
