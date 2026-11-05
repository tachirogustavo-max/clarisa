using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Infrastructure.Configuration;

namespace Clarisa.Infrastructure.Persistence;

// Misma base, mismo usuario de aplicación y mismo turno de escritura que las unidades académicas.
public sealed class FabricaUnidadSeguridadMariaDb(ConfiguracionMariaDb configuracion, IReferenciaSesionActual? solicitud = null, IReloj? reloj = null) : IFabricaUnidadSeguridad
{
    private readonly AperturaUnidadMariaDb apertura = new(configuracion);

    public async Task<IUnidadSeguridad> IniciarAsync(ModoUnidadTrabajo modo, CancellationToken ct)
    {
        var referencia = solicitud?.Obtener();
        return await apertura.AbrirAsync(modo, (conexion, transaccion, modoUnidad, segundos) =>
        {
            var unidad = new UnidadSeguridadMariaDb(conexion, transaccion, modoUnidad, segundos);
            if (referencia is not null) unidad.VincularAcceso(referencia, reloj ?? throw new InvalidOperationException("Falta el reloj del servidor."));
            return unidad;
        }, ct);
    }
}
