using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Domain.Common;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Calificaciones;

internal sealed record ContextoCalificacion(Seccion Seccion, Materia Materia, Lapso Lapso);

internal sealed record ReglasEvaluacion(ModalidadEvaluacion? Modalidad, IReadOnlyList<LetraCualitativa> Letras)
{
    public bool LetraValida(string letra) => Letras.Any(registrada => registrada.Letra == letra);
}

internal static class AccesoCalificaciones
{
    // Se ejecuta dentro de la unidad del caso: la desactivación del docente o el retiro de su asignación quedan serializados con la escritura.
    public static async Task<ContextoCalificacion> ResolverAsync(IUnidadTrabajo unidad, Actor actor, Guid seccionId, Guid materiaId,
        Guid lapsoId, bool exigirMateriaActiva, CancellationToken ct)
    {
        if (!Autorizacion.EsAdministracion(actor))
        {
            var docenteId = await Autorizacion.ExigirDocenteActivoAsync(actor, unidad, ct);
            if (!await unidad.AsignacionesDocentes.ExisteAsync(seccionId, materiaId, docenteId, ct))
                throw Error.AccesoDenegado("Solo puede trabajar con las materias y secciones que tiene asignadas.");
        }

        var seccion = await unidad.Secciones.ObtenerAsync(seccionId, ct) ?? throw Error.NoEncontrado("La sección no existe.");
        var materia = await unidad.Materias.ObtenerAsync(materiaId, ct)
            ?? throw Error.ReferenciaInvalida("La materia no existe.", nameof(GuardarCalificaciones.MateriaId));
        if (exigirMateriaActiva && !materia.Activa)
            throw Error.ReferenciaInvalida("La materia está inactiva.", nameof(GuardarCalificaciones.MateriaId));
        var asignaciones = await unidad.AsignacionesDocentes.ListarPorSeccionAsync(seccion.Id, ct);
        if (!asignaciones.Any(asignacion => asignacion.MateriaId == materia.Id))
            throw Error.ReferenciaInvalida("La materia no está asignada a esta sección.", nameof(GuardarCalificaciones.MateriaId));

        var lapso = await unidad.Lapsos.ObtenerAsync(lapsoId, ct)
            ?? throw Error.ReferenciaInvalida("El momento no existe.", nameof(GuardarCalificaciones.LapsoId));
        if (lapso.AnioLectivoId != seccion.AnioLectivoId)
            throw Error.DatosInvalidos("El momento pertenece a otro año lectivo.", nameof(GuardarCalificaciones.LapsoId));
        return new ContextoCalificacion(seccion, materia, lapso);
    }

    public static async Task<ReglasEvaluacion> EvaluacionAsync(IUnidadTrabajo unidad, ContextoCalificacion contexto, CancellationToken ct)
    {
        var plan = await unidad.PlanEstudios.BuscarAsync(contexto.Seccion.AnioLectivoId, contexto.Seccion.Grado, contexto.Materia.Id, ct);
        var usaLetras = plan?.Modalidad is ModalidadEvaluacion.Cualitativa or ModalidadEvaluacion.NumericaYCualitativa;
        var letras = usaLetras ? await unidad.EscalaCualitativa.ListarAsync(contexto.Seccion.AnioLectivoId, ct) : [];
        return new ReglasEvaluacion(plan?.Modalidad, letras);
    }

    // La ventana y la hora se leen dentro de la misma unidad que confirma; la administración tampoco la omite.
    public static async Task<(VentanaCarga Ventana, DateTimeOffset Ahora)> ExigirVentanaAsync(IUnidadTrabajo unidad, IReloj reloj,
        Guid lapsoId, CancellationToken ct)
    {
        var ventana = await unidad.VentanasCarga.ObtenerPorLapsoAsync(lapsoId, ct);
        var ahora = Instantes.Utc(reloj.Ahora);
        if (ventana is null || !ventana.PermiteCargaEn(ahora)) throw Error.VentanaCerrada();
        return (ventana, ahora);
    }

    public static void ExigirVentanaVigente(VentanaCarga ventana, IReloj reloj)
    {
        if (!ventana.PermiteCargaEn(Instantes.Utc(reloj.Ahora))) throw Error.VentanaCerrada();
    }
}
