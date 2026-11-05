using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Design;
public sealed class DatosSecciones : ModeloSecciones
{
    public DatosSecciones()
    {
        EstablecerAnios(ReferenciaDiseno.Anios);
        DatosDiseno([new SeccionRemota(ReferenciaDiseno.SeccionId, ReferenciaDiseno.AnioId, "1er año", "A", 1)]);
    }
}
