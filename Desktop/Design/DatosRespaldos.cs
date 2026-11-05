using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Design;
public sealed class DatosRespaldos : ModeloRespaldos
{
    public DatosRespaldos() => DatosDiseno([new RespaldoRemoto(Guid.NewGuid(), "Respaldo-2026-10-30", new(2026, 10, 30, 17, 0, 0, TimeSpan.FromHours(-4)), 14_500_000, true), new RespaldoRemoto(Guid.NewGuid(), "Respaldo-2026-10-29", new(2026, 10, 29, 17, 0, 0, TimeSpan.FromHours(-4)), 14_300_000, true)]);
}
