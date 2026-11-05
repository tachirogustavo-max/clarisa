using Clarisa.Instalacion.Ejecucion;

namespace Clarisa.Instalacion.Pasos;

// Pasos que solo comprueban: si la condición no se cumple, se rechaza antes de modificar nada.
internal abstract class PasoVerificacion : IPasoInstalacion
{
    public abstract string Id { get; }
    public abstract string Nombre { get; }
    public abstract string Descripcion { get; }
    public bool Critico => true;
    public bool Privilegiado => false;

    public abstract Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct);

    public async Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var resultado = await ComprobarAsync(contexto, ct);
        throw new FalloInstalacionException(resultado.Detalle);
    }
}
