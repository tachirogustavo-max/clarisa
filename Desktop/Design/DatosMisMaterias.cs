using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Design;
public sealed class DatosMisMaterias : ModeloMisMaterias
{
    private static readonly ServiciosPlanillaDiseno Servicios = new();
    public DatosMisMaterias() : this(false)
    {
    }

    public DatosMisMaterias(bool administracion) : base(administracion, null, Servicios, Servicios, Servicios)
    {
        EstablecerOpciones(ReferenciaDiseno.Anios, ReferenciaDiseno.Lapsos);
        DatosDiseno([new AsignacionRemota(Guid.NewGuid(), ReferenciaDiseno.SeccionId, ReferenciaDiseno.MateriaId, ReferenciaDiseno.DocenteId, "1er año · A", "Matemática", "Pérez, Ana María", 1)]);
    }
}
