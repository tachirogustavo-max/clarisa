using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Design;
public sealed class DatosPlanilla : ModeloPlanilla
{
    public DatosPlanilla() : this(false)
    {
    }

    public DatosPlanilla(bool cerrada) : base(new ClienteDiseno(), new ContextoPlanilla(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), new[] { new FilaNota(Guid.NewGuid(), "Valeria Núñez", "10000001", 18, 1, 2), new FilaNota(Guid.NewGuid(), "Mateo Rivero", "10000002", null, 0, 2), new FilaNota(Guid.NewGuid(), "Adrián Flores", "10000003", 16, 1, 2), new FilaNota(Guid.NewGuid(), "Lucía Mendoza", "10000004", 19, 1, 2), new FilaNota(Guid.NewGuid(), "Isabel Suárez", "10000005", null, 0, 2), new FilaNota(Guid.NewGuid(), "Gabriel Rojas", "10000006", 15, 1, 2) }, cargaPermitida: !cerrada)
    {
        Materia = "Castellano";
        Seccion = "1er año · Sección B";
        Lapso = "Primer momento";
        Filas[2].Texto = "17";
    }

    private sealed class ClienteDiseno : IClientePlanillas
    {
        public Task<RespuestaGuardado> GuardarAsync(ContextoPlanilla contexto, IReadOnlyList<NotaParaGuardar> notas, CancellationToken ct) => Task.FromResult(new RespuestaGuardado([], FalloGuardado.Rechazado, "Los datos de diseño no se envían a un servidor."));
    }
}
