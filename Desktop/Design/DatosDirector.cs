using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Design;
public sealed class DatosDirector : ModeloDirector
{
    public DatosDirector() : base(new ClienteDiseno(), new[] { new LapsoVisual(Guid.NewGuid(), "Primer momento", 1, true, true, 1), new LapsoVisual(Guid.NewGuid(), "Segundo momento", 2, false, false, 0), new LapsoVisual(Guid.NewGuid(), "Tercer momento", 3, false, false, 0) }) => AnioEscolar = "2026–2027";
    private sealed class ClienteDiseno : IClienteVentanas
    {
        public Task<RespuestaVentana> ConsultarAsync(Guid lapsoId, CancellationToken ct) => Task.FromResult(new RespuestaVentana(null, "Los datos de diseño no consultan un servidor."));
        public Task<RespuestaVentana> CambiarAsync(Guid lapsoId, bool abrir, long versionEsperada, CancellationToken ct) => Task.FromResult(new RespuestaVentana(null, "Los datos de diseño no modifican un servidor."));
    }
}
