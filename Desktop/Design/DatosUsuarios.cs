using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Design;
public sealed class DatosUsuarios : ModeloUsuarios
{
    public DatosUsuarios()
    {
        EstablecerDocentes(ReferenciaDiseno.Docentes);
        DatosDiseno([new CuentaRemota(Guid.NewGuid(), "direccion", "Dirección del liceo", "Administracion", null, true, 1), new CuentaRemota(Guid.NewGuid(), "ana.perez", "Ana María Pérez", "Docente", ReferenciaDiseno.DocenteId, true, 1)]);
    }
}
