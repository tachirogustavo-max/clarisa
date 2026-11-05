using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.ConfiguracionAcademica;

public sealed record AgregarLetraCualitativa(Guid Id, Guid AnioLectivoId, string? Letra, string? Descripcion);

public sealed record LetraCualitativaDatos(Guid Id, Guid AnioLectivoId, string Letra, string? Descripcion, long Version)
{
    public static LetraCualitativaDatos Desde(LetraCualitativa letra) =>
        new(letra.Id, letra.AnioLectivoId, letra.Letra, letra.Descripcion, letra.Version);
}

public sealed record ConfigurarPlanMateria(Guid Id, Guid AnioLectivoId, string? Grado, Guid MateriaId, ModalidadEvaluacion Modalidad,
    long VersionEsperada);

public sealed record PlanMateriaDatos(Guid Id, Guid AnioLectivoId, string Grado, Guid MateriaId, ModalidadEvaluacion Modalidad, long Version)
{
    public static PlanMateriaDatos Desde(PlanMateria plan) =>
        new(plan.Id, plan.AnioLectivoId, plan.Grado, plan.MateriaId, plan.Modalidad, plan.Version);
}
