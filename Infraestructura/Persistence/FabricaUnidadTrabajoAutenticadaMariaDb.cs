using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Application.Common.Errores;
using Clarisa.Infrastructure.Configuration;

namespace Clarisa.Infrastructure.Persistence;

public sealed class FabricaUnidadTrabajoAutenticadaMariaDb(ConfiguracionMariaDb configuracion, IReferenciaSesionActual solicitud, IReloj reloj) : IFabricaUnidadTrabajo
{
    private readonly AperturaUnidadMariaDb apertura = new(configuracion);
    public async Task<IUnidadTrabajo> IniciarAsync(ModoUnidadTrabajo modo, CancellationToken ct)
    {
        var referencia = solicitud.Obtener() ?? throw Error.SesionNoValida();
        return await apertura.AbrirAsync(modo, (conexion, transaccion, modoUnidad, segundos) =>
        {
            var unidad = new UnidadTrabajoMariaDb(conexion, transaccion, modoUnidad, segundos);
            unidad.VincularAcceso(referencia, reloj);
            return unidad;
        }, ct);
    }
}
