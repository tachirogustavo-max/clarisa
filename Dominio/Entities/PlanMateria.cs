using Clarisa.Domain.Common;
using Clarisa.Domain.Exceptions;

namespace Clarisa.Domain.Entities;

// Una materia del plan de un grado en un año: indica si se cursa y cómo se evalúa. La ausencia de fila no significa «no cursada».
public sealed class PlanMateria : Entidad
{
    public PlanMateria(Guid id, Guid anioLectivoId, string? grado, Guid materiaId, ModalidadEvaluacion modalidad, long version)
        : base(id, version)
    {
        AnioLectivoId = Requerido(anioLectivoId, "AnioLectivoId", "el año lectivo");
        Grado = Textos.Requerido(grado, "Grado", Seccion.MaximoGrado);
        MateriaId = Requerido(materiaId, "MateriaId", "la materia");
        Modalidad = Validar(modalidad);
    }

    public Guid AnioLectivoId { get; }
    public string Grado { get; }
    public string GradoClave => Textos.Clave(Grado);
    public Guid MateriaId { get; }
    public ModalidadEvaluacion Modalidad { get; private set; }

    public void CambiarModalidad(ModalidadEvaluacion modalidad) => Modalidad = Validar(modalidad);

    public bool Corresponde(Seccion seccion) => seccion.AnioLectivoId == AnioLectivoId && Textos.Iguales(seccion.Grado, Grado);

    private static ModalidadEvaluacion Validar(ModalidadEvaluacion modalidad) =>
        Enum.IsDefined(modalidad) ? modalidad : throw new ReglaDominioException("Modalidad", "Selecciona una forma de evaluación válida.");
}
