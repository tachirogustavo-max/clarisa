using Clarisa.Desktop.Presentation.Models;

namespace Clarisa.Desktop.Design;
public sealed class DatosVentanas : ModeloVentanas
{
    public DatosVentanas()
    {
        Anios = ReferenciaDiseno.Anios;
        Anio = Anios[0];
        Director = new DatosDirector();
    }
}
