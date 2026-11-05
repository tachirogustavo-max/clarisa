using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Design;
public sealed class DatosAnios : ModeloAnios
{
    public DatosAnios() => DatosDiseno([new AnioRemoto(ReferenciaDiseno.AnioId, "2026–2027", new(2026, 9, 15), new(2027, 7, 30), ReferenciaDiseno.LapsoId, 1)]);
}
