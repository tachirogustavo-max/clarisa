using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Design;
public sealed class DatosAsignaciones : ModeloAsignaciones
{
    public DatosAsignaciones()
    {
        EstablecerOpcionesAnio(ReferenciaDiseno.Anios);
        EstablecerOpciones(ReferenciaDiseno.Secciones, ReferenciaDiseno.Materias, ReferenciaDiseno.Docentes);
        DatosDiseno([new AsignacionRemota(Guid.NewGuid(), ReferenciaDiseno.SeccionId, ReferenciaDiseno.MateriaId, ReferenciaDiseno.DocenteId, "1er año · A", "Matemática", "Pérez, Ana María", 1)]);
    }
}
