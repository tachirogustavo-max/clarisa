using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Design;
public sealed class DatosMaterias : ModeloMaterias
{
    public DatosMaterias() : base(configurarEvaluacion: true)
    {
        EstablecerAnios([new(Guid.NewGuid(), "2026-2027")]);
        DatosDiseno([new(Guid.NewGuid(), "MAT", "Matemática", true, 1), new(Guid.NewGuid(), "CAST", "Castellano", true, 1), new(Guid.NewGuid(), "ING", "Inglés", true, 1), new(Guid.NewGuid(), "EF", "Educación física", true, 1)]);
    }
}
