using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Application.Features.CierresResultados;
using Clarisa.Application.Features.ConfiguracionAcademica;
using Clarisa.Application.Features.Inscripciones;
using Clarisa.Domain.Common;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Calificaciones;

public sealed class GestionCalificaciones(IFabricaUnidadTrabajo fabrica, IReloj reloj)
{
    public async Task<PlanillaCalificaciones> PlanillaAsync(Actor actor, ConsultarPlanilla solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirPersonal(actor);
        var datos = Error.SolicitudRequerida(solicitud);

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var contexto = await AccesoCalificaciones.ResolverAsync(unidad, actor, datos.SeccionId, datos.MateriaId, datos.LapsoId,
            exigirMateriaActiva: false, ct);
        var reglas = await AccesoCalificaciones.EvaluacionAsync(unidad, contexto, ct);
        var ventana = await unidad.VentanasCarga.ObtenerPorLapsoAsync(contexto.Lapso.Id, ct);

        var inscritos = await Nominas.ActivosAsync(unidad, contexto.Seccion.Id, ct);
        var notas = (await unidad.Calificaciones.ListarAsync(contexto.Lapso.Id, contexto.Materia.Id,
                inscritos.Select(inscrito => inscrito.Inscripcion.Id).ToList(), ct))
            .ToDictionary(calificacion => calificacion.InscripcionId);
        var filas = inscritos.Select(inscrito =>
        {
            var nota = notas.GetValueOrDefault(inscrito.Inscripcion.Id);
            return new FilaPlanilla(inscrito.Inscripcion.Id, inscrito.Estudiante.Id, inscrito.Estudiante.Identificacion,
                inscrito.Estudiante.Nombres, inscrito.Estudiante.Apellidos, nota?.Valor, nota?.Version ?? 0)
            {
                Tipo = nota?.Tipo ?? TipoResultado.Pendiente,
                Letra = nota?.Letra,
                MotivoExoneracion = Autorizacion.EsAdministracion(actor) ? nota?.MotivoExoneracion : null,
                Inasistencias = nota?.Inasistencias
            };
        }).ToList();

        var ahora = Instantes.Utc(reloj.Ahora);
        var politica = await unidad.Politicas.VigenteAsync(contexto.Seccion.AnioLectivoId, ct);
        var cerrada = await unidad.Cierres.VigenteAsync(contexto.Seccion.Id, ct) is not null;
        var cargaPermitida = !cerrada && contexto.Materia.Activa && ReglasModalidad.AdmiteCarga(reglas.Modalidad)
            && (ventana?.PermiteCargaEn(ahora) ?? false);
        return new PlanillaCalificaciones(contexto.Seccion.Id, contexto.Materia.Id, contexto.Lapso.Id, cargaPermitida,
            cargaPermitida ? ventana!.Hasta : null, ahora, LoteCalificaciones.DecimalesCaptura(politica), filas)
        {
            Modalidad = reglas.Modalidad,
            Letras = reglas.Letras.Select(LetraCualitativaDatos.Desde).ToList(),
            ResultadosCerrados = cerrada
        };
    }

    public async Task<ResultadoGuardado> GuardarAsync(Actor actor, GuardarCalificaciones solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirPersonal(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        var notas = LoteCalificaciones.Validar(datos);

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var contexto = await AccesoCalificaciones.ResolverAsync(unidad, actor, datos.SeccionId, datos.MateriaId, datos.LapsoId,
            exigirMateriaActiva: true, ct);
        var (ventana, ahora) = await AccesoCalificaciones.ExigirVentanaAsync(unidad, reloj, contexto.Lapso.Id, ct);
        await GestionCierres.ExigirSinCierreAsync(unidad, contexto.Seccion.Id, ct);
        // La política se lee en la misma unidad que confirma: su activación y este guardado quedan ordenados por el turno.
        LoteCalificaciones.ExigirDecimales(notas, LoteCalificaciones.DecimalesCaptura(
            await unidad.Politicas.VigenteAsync(contexto.Seccion.AnioLectivoId, ct)));
        LoteCalificaciones.ExigirModalidad(notas, await AccesoCalificaciones.EvaluacionAsync(unidad, contexto, ct));

        await LoteCalificaciones.ExigirInscripcionesAsync(unidad, contexto.Seccion.Id, notas, ct);
        var guardadas = (await unidad.Calificaciones.ListarAsync(contexto.Lapso.Id, contexto.Materia.Id,
                notas.Select(nota => nota.InscripcionId).ToList(), ct))
            .ToDictionary(calificacion => calificacion.InscripcionId);
        LoteCalificaciones.ExigirVersiones(notas, guardadas);

        var resultado = new List<NotaGuardada>(notas.Count);
        foreach (var nota in notas)
        {
            var actual = guardadas.GetValueOrDefault(nota.InscripcionId);
            resultado.Add(actual is { Exonerada: true } || LoteCalificaciones.SinCambio(nota, actual)
                ? RegistroResultados.Guardada(nota.InscripcionId, actual, actual?.Version ?? 0, cambio: false)
                : await RegistroResultados.AplicarAsync(unidad, reloj, actor, contexto, nota.InscripcionId, actual,
                    calificacion => calificacion.Registrar(nota.Valor, nota.Letra, LoteCalificaciones.Inasistencias(nota, actual)),
                    AccionAuditoria.CambioCalificacion, motivo: null,
                    ahora, ct));
        }

        AccesoCalificaciones.ExigirVentanaVigente(ventana, reloj);
        await unidad.ConfirmarAsync(ct);
        return new ResultadoGuardado(resultado, resultado.Count(nota => nota.Cambio));
    }
}
