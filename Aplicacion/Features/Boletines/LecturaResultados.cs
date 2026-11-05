using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Features.ConfiguracionAcademica;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Boletines;

internal sealed record DatosSeccion(Seccion Seccion, AnioLectivo Anio, IReadOnlyList<Lapso> Lapsos, IReadOnlySet<Guid> MateriasAsignadas,
    IReadOnlyDictionary<Guid, ModalidadEvaluacion> Plan)
{
    public IEnumerable<Guid> MateriasPrevistas => MateriasAsignadas.Concat(Plan.Keys);
}

// Lectura común del boletín y del cierre: los mismos resultados por materia y lapso, con la definitiva de la política indicada.
internal static class LecturaResultados
{
    public static async Task<DatosSeccion> LeerSeccionAsync(IUnidadTrabajo unidad, Guid seccionId, CancellationToken ct)
    {
        var seccion = await unidad.Secciones.ObtenerAsync(seccionId, ct) ?? throw Error.NoEncontrado("La sección no existe.");
        var anio = await unidad.AniosLectivos.ObtenerAsync(seccion.AnioLectivoId, ct)
            ?? throw Error.ReferenciaInvalida("El año lectivo de la sección no existe.");
        var lapsos = (await unidad.Lapsos.ListarPorAnioAsync(anio.Id, ct)).OrderBy(lapso => lapso.Orden).ToList();
        var asignadas = (await unidad.AsignacionesDocentes.ListarPorSeccionAsync(seccion.Id, ct))
            .Select(asignacion => asignacion.MateriaId).ToHashSet();
        var plan = (await unidad.PlanEstudios.ListarAsync(anio.Id, seccion.Grado, GestionPlanEstudios.MaximoListado, ct))
            .ToDictionary(entrada => entrada.MateriaId, entrada => entrada.Modalidad);
        return new DatosSeccion(seccion, anio, lapsos, asignadas, plan);
    }

    public static async Task<IReadOnlyDictionary<Guid, Materia>> LeerMateriasAsync(IUnidadTrabajo unidad, IEnumerable<Guid> ids,
        CancellationToken ct)
    {
        var distintas = ids.Distinct().ToArray();
        var materias = (await unidad.Materias.ObtenerVariasAsync(distintas, ct)).ToDictionary(m => m.Id);
        if (materias.Count != distintas.Length) throw Error.ReferenciaInvalida("Una asignación o calificación apunta a una materia inexistente.");
        return materias;
    }

    // Resultados actuales de una inscripción: materias previstas por plan o asignación y las que conservan una nota anterior.
    public static IReadOnlyList<ResultadoCerrado> Actuales(DatosSeccion datos, Guid inscripcionId, IReadOnlyCollection<Calificacion> calificaciones,
        PoliticaAcademica? politica)
    {
        var notas = calificaciones.Where(c => c.InscripcionId == inscripcionId).ToDictionary(c => (c.MateriaId, c.LapsoId));
        return datos.MateriasPrevistas.Concat(notas.Keys.Select(clave => clave.MateriaId)).Distinct().Select(materiaId =>
        {
            ModalidadEvaluacion? modalidad = datos.Plan.TryGetValue(materiaId, out var configurada) ? configurada : null;
            var lapsos = datos.Lapsos.Select(lapso => notas.GetValueOrDefault((materiaId, lapso.Id)) is { } nota
                ? new LapsoCerrado(lapso.Orden, nota.Tipo, nota.Valor, nota.Letra, nota.Version)
                : new LapsoCerrado(lapso.Orden, TipoResultado.Pendiente, null, null, 0)).ToList();
            var definitiva = CalculoDefinitiva.Calcular(politica, modalidad,
                lapsos.Select(l => new ResultadoLapso(l.Orden, l.Tipo, l.Valor)).ToList());
            return new ResultadoCerrado(inscripcionId, materiaId, modalidad, lapsos, definitiva);
        }).ToList();
    }

    // Una materia no cursada se muestra como tal salvo que conserve un resultado anterior al plan: ese dato no se oculta.
    public static EstadoResultadoBoletin Estado(ModalidadEvaluacion? modalidad, TipoResultado tipo) =>
        tipo == TipoResultado.Pendiente && modalidad == ModalidadEvaluacion.NoCursada
            ? EstadoResultadoBoletin.NoCursada
            : (EstadoResultadoBoletin)(int)tipo;
}
