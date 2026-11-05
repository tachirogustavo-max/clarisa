using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Design;

// Nombres ficticios solo para el diseñador.
public sealed class DatosFirmantes : ModeloFirmantes
{
    public DatosFirmantes()
    {
        Mostrar(new FirmantesRemotos(new NombresFirmantesRemotos("Nombre de diseño uno", "Nombre de diseño dos", null), 0, null, null));
        Informar("Datos de diseño ficticios.");
    }
}
