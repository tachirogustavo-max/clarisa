using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Errores;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.ConfiguracionAcademica;

internal static class CompatibilidadPlan
{
    public static async Task ExigirGradoAsync(IUnidadTrabajo unidad, Guid anio, string grado, CancellationToken ct)
    {
        if (!await unidad.Secciones.ExisteGradoAsync(anio, grado, ct))
            throw Error.ReferenciaInvalida("Selecciona un grado existente en las secciones del año. Si aún no existe, crea primero su sección.", "Grado");
    }
    public static async Task ExigirCapacidadAsync(IUnidadTrabajo unidad, Guid anio, CancellationToken ct)
    {
        if (await unidad.PlanEstudios.ContarAsync(anio, ct) >= GestionPlanEstudios.MaximoListado)
            throw Error.EstadoNoPermitido("El año ya alcanzó el límite de configuraciones de materias. Revisa el plan antes de agregar otra.");
    }
    // Se lee dentro de la unidad de escritura: el turno global de escritores impide que entre una nota incompatible mientras tanto.
    public static async Task ExigirAsync(IUnidadTrabajo unidad, Guid anioLectivoId, string grado, Guid materiaId,
        ModalidadEvaluacion? nueva, CancellationToken ct)
    {
        var conteo = await unidad.Calificaciones.ContarPorTipoEnGradoAsync(anioLectivoId, grado, materiaId, ct);
        var incompatibles = conteo.Where(par => par.Value > 0 && !ReglasModalidad.Admite(nueva, par.Key))
            .OrderBy(par => par.Key).ToList();
        if (incompatibles.Count == 0) return;
        var detalle = string.Join(", ", incompatibles.Select(par => $"{par.Value} {Describir(par.Key)}"));
        throw Error.EnUso($"Hay resultados registrados que no corresponden a esa forma de evaluación ({detalle}). "
            + "Corríjalos antes de cambiar el plan; no se modifican automáticamente.");
    }

    private static string Describir(TipoResultado tipo) => tipo switch
    {
        TipoResultado.Numerico => "numéricos",
        TipoResultado.Cualitativo => "cualitativos",
        TipoResultado.NumericoYCualitativo => "numéricos con letra",
        TipoResultado.Exonerado => "exonerados",
        _ => "pendientes"
    };
}
