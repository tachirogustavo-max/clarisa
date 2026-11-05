using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Application.Features.CierresResultados;
using Clarisa.Application.Features.Inscripciones;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Calificaciones;

// La exoneración es un acto de la dirección con motivo; se exige la misma ventana de carga que las notas hasta que el liceo indique otra cosa.
public sealed class GestionExoneraciones(IFabricaUnidadTrabajo fabrica, IReloj reloj)
{
    private sealed record Celda(Guid SeccionId, Guid MateriaId, Guid LapsoId, Guid InscripcionId, long VersionEsperada);

    public Task<NotaGuardada> ExonerarAsync(Actor actor, Exonerar solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        var motivo = Error.DesdeDominio(() => Calificacion.NormalizarMotivo(datos.Motivo));
        return EjecutarAsync(actor, new Celda(datos.SeccionId, datos.MateriaId, datos.LapsoId, datos.InscripcionId, datos.VersionEsperada),
            actual => actual is { Exonerada: true } && actual.MotivoExoneracion == motivo,
            calificacion => calificacion.Exonerar(motivo), AccionAuditoria.Exoneracion, motivo, ct);
    }

    public Task<NotaGuardada> RevocarAsync(Actor actor, RevocarExoneracion solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        var motivo = Error.DesdeDominio(() => Calificacion.NormalizarMotivo(datos.Motivo));
        return EjecutarAsync(actor, new Celda(datos.SeccionId, datos.MateriaId, datos.LapsoId, datos.InscripcionId, datos.VersionEsperada),
            sinCambio: _ => false, calificacion => calificacion.RevocarExoneracion(), AccionAuditoria.RevocacionExoneracion, motivo, ct);
    }

    private async Task<NotaGuardada> EjecutarAsync(Actor actor, Celda celda, Func<Calificacion?, bool> sinCambio,
        Action<Calificacion> cambiar, AccionAuditoria accion, string motivo, CancellationToken ct)
    {
        if (celda.InscripcionId == Guid.Empty) throw Error.DatosInvalidos("Selecciona el estudiante.", nameof(Exonerar.InscripcionId));
        if (celda.VersionEsperada < 0) throw Error.DatosInvalidos("La versión esperada no es válida.", nameof(Exonerar.VersionEsperada));

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var contexto = await AccesoCalificaciones.ResolverAsync(unidad, actor, celda.SeccionId, celda.MateriaId, celda.LapsoId,
            exigirMateriaActiva: true, ct);
        var (ventana, ahora) = await AccesoCalificaciones.ExigirVentanaAsync(unidad, reloj, contexto.Lapso.Id, ct);
        await GestionCierres.ExigirSinCierreAsync(unidad, contexto.Seccion.Id, ct);
        var reglas = await AccesoCalificaciones.EvaluacionAsync(unidad, contexto, ct);
        if (!ReglasModalidad.Admite(reglas.Modalidad, TipoResultado.Exonerado))
            throw Error.EstadoNoPermitido("Esta materia no se cursa en el grado según el plan del año.", nameof(Exonerar.MateriaId));

        var inscripcion = await unidad.Inscripciones.ObtenerAsync(celda.InscripcionId, ct);
        var estudiantes = inscripcion is null ? [] : await Nominas.EstudiantesDeAsync(unidad, [inscripcion], ct);
        LoteCalificaciones.ExigirInscripcion(inscripcion, estudiantes, contexto.Seccion.Id, nameof(Exonerar.InscripcionId));

        var actual = (await unidad.Calificaciones.ListarAsync(contexto.Lapso.Id, contexto.Materia.Id, [celda.InscripcionId], ct))
            .SingleOrDefault();
        if (sinCambio(actual)) return RegistroResultados.Guardada(celda.InscripcionId, actual, actual!.Version, cambio: false);
        if ((actual?.Version ?? 0) != celda.VersionEsperada) throw Error.VersionObsoleta(nameof(Exonerar.VersionEsperada));
        if (accion == AccionAuditoria.RevocacionExoneracion && actual is not { Exonerada: true })
            throw Error.EstadoNoPermitido("El resultado no está exonerado.", nameof(Exonerar.InscripcionId));

        var resultado = await RegistroResultados.AplicarAsync(unidad, reloj, actor, contexto, celda.InscripcionId, actual, cambiar, accion,
            motivo, ahora, ct);
        AccesoCalificaciones.ExigirVentanaVigente(ventana, reloj);
        await unidad.ConfirmarAsync(ct);
        return resultado;
    }
}
