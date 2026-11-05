using Clarisa.Desktop.Services;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Design;
internal sealed class ServiciosPlanillaDiseno : IClienteLecturas, IClienteOpciones, IClientePlanillas
{
    public Task<ResultadoRemoto<IReadOnlyList<OpcionRemota>>> ConsultarAsync(TipoOpciones tipo, Guid? anio, Guid? seccion, CancellationToken ct) => Task.FromResult(new ResultadoRemoto<IReadOnlyList<OpcionRemota>>(tipo switch
    {
        TipoOpciones.Anios => ReferenciaDiseno.Anios,
        TipoOpciones.Lapsos => ReferenciaDiseno.Lapsos,
        _ => Array.Empty<OpcionRemota>()}, null));
    public Task<ResultadoRemoto<T>> ConsultarAsync<T>(string ruta, CancellationToken ct)
    {
        if (typeof(T) != typeof(PlanillaRemota))
            return Task.FromResult(new ResultadoRemoto<T>(default, "Consulta no disponible en datos de diseño."));
        var datos = new PlanillaRemota(ReferenciaDiseno.SeccionId, ReferenciaDiseno.MateriaId, ReferenciaDiseno.LapsoId, true, 2, [new(Guid.NewGuid(), ReferenciaDiseno.EstudianteId, "12345678", "Ana María", "Mendoza Rojas", 18, 1), new(Guid.NewGuid(), Guid.NewGuid(), "23456789", "José Antonio", "Torres Salas", null, 0)]);
        return Task.FromResult(new ResultadoRemoto<T>((T)(object)datos, null));
    }

    public Task<RespuestaGuardado> GuardarAsync(ContextoPlanilla contexto, IReadOnlyList<NotaParaGuardar> notas, CancellationToken ct) => Task.FromResult(new RespuestaGuardado([], FalloGuardado.Rechazado, "Esta vista utiliza datos ficticios y no guarda notas en un servidor."));
}
