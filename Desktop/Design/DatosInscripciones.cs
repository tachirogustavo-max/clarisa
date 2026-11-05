using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Design;
public sealed class DatosInscripciones : ModeloInscripciones
{
    public DatosInscripciones()
    {
        EstablecerOpcionesAnio(ReferenciaDiseno.Anios);
        EstablecerOpciones(ReferenciaDiseno.Estudiantes, ReferenciaDiseno.Secciones);
        DatosDiseno([new InscripcionRemota(Guid.NewGuid(), ReferenciaDiseno.EstudianteId, ReferenciaDiseno.SeccionId, "Mendoza Rojas, Ana María", "12345678", "1er año · A", new(2026, 9, 1), "Activa", null, 1)]);
    }
}
