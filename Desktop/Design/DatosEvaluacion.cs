using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Design;
public sealed class DatosEscalaAnual : ModeloEscalaAnual
{
    public DatosEscalaAnual()
    {
        EstablecerOpcionesAnio(ReferenciaDiseno.Anios);
        DatosDiseno([new LetraRemota(Guid.NewGuid(), ReferenciaDiseno.AnioId, "A", "Descripción autorizada por el liceo", 1)]);
    }
}
public sealed class DatosPlanEstudios : ModeloPlanEstudios
{
    public DatosPlanEstudios()
    {
        Materias = ReferenciaDiseno.Materias;
        Grados = ["1er año"];
        EstablecerOpcionesAnio(ReferenciaDiseno.Anios);
        DatosDiseno([new PlanMateriaRemota(Guid.NewGuid(), ReferenciaDiseno.AnioId, "1er año", ReferenciaDiseno.MateriaId, 4, 1) { Materia = "Orientación y Convivencia" },
            new PlanMateriaRemota(Guid.NewGuid(), ReferenciaDiseno.AnioId, "1er año", Guid.NewGuid(), 1, 1) { Materia = "Física" }]);
    }
}
public sealed class DatosPlanillaTipada : ModeloPlanilla
{
    public DatosPlanillaTipada() : this(false) { }
    public DatosPlanillaTipada(bool mixta) : base(new GuardadorDiseno(), new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
        CrearFilas(mixta), true, exoneraciones: new ExoneracionDiseno())
    {
        Materia = mixta ? "Orientación y Convivencia" : "Grupos de Creación, Recreación y Producción";
        Seccion = "1er año · Sección B";
        Lapso = "Primer momento";
    }
    private static IEnumerable<FilaNota> CrearFilas(bool mixta)
    {
        string[] nombres = ["Mendoza Rojas, Ana María", "Pérez, Luis Alberto", "Romero Díaz, Sofía", "Torres Salas, Miguel", "Rivero, Camila"];
        return nombres.Select((nombre, i) => new FilaNota(Guid.NewGuid(), nombre, "1234567" + i,
            mixta && i is 0 or 3 ? 18 : null, i == 2 ? 0 : 1, 2,
            new(mixta ? 4 : 3, i == 1 ? 4 : i == 2 ? 0 : mixta ? 3 : 2, i is 1 or 2 ? null : "A", ["A"])));
    }
    private sealed class GuardadorDiseno : IClientePlanillas
    {
        public Task<RespuestaGuardado> GuardarAsync(ContextoPlanilla contexto, IReadOnlyList<NotaParaGuardar> notas, CancellationToken ct) =>
            Task.FromResult(new RespuestaGuardado([], FalloGuardado.Rechazado, "Los datos de diseño no se envían."));
    }
    private sealed class ExoneracionDiseno : IClienteExoneraciones
    {
        public Task<ResultadoRemoto<NotaConfirmada>> CambiarAsync(ContextoPlanilla contexto, Guid inscripcion, long version, string motivo, bool revocar, CancellationToken ct) =>
            Task.FromResult(new ResultadoRemoto<NotaConfirmada>(null, "Los datos de diseño no se envían."));
    }
}
