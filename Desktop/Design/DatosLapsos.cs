using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Design;
public sealed class DatosLapsos : ModeloLapsos
{
    public DatosLapsos()
    {
        EstablecerAnios(ReferenciaDiseno.Anios);
        DatosDiseno([new LapsoRemoto(ReferenciaDiseno.LapsoId, ReferenciaDiseno.AnioId, "Primer momento", 1, new(2026, 9, 15), new(2026, 12, 15), true, 1), new LapsoRemoto(Guid.NewGuid(), ReferenciaDiseno.AnioId, "Segundo momento", 2, new(2027, 1, 7), new(2027, 4, 1), false, 1), new LapsoRemoto(Guid.NewGuid(), ReferenciaDiseno.AnioId, "Tercer momento", 3, new(2027, 4, 5), new(2027, 7, 30), false, 1)]);
    }
}
