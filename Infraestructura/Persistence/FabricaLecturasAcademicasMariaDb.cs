using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Application.Common.Errores;
using Clarisa.Infrastructure.Configuration;

namespace Clarisa.Infrastructure.Persistence;

public sealed class FabricaLecturasAcademicasMariaDb(ConfiguracionMariaDb configuracion, IReferenciaSesionActual? solicitud = null, IReloj? reloj = null) : IFabricaLecturasAcademicas
{
    private readonly AperturaUnidadMariaDb apertura = new(configuracion);
    public async Task<IUnidadLecturasAcademicas> IniciarAsync(CancellationToken ct)
    {
        var referencia = solicitud?.Obtener();
        if (solicitud is not null && referencia is null) throw Error.SesionNoValida();
        return await apertura.AbrirAsync(ModoUnidadTrabajo.Lectura, (conexion, transaccion, _, segundos) =>
        {
            var unidad = new UnidadLecturasAcademicasMariaDb(conexion, transaccion, segundos);
            if (referencia is not null) unidad.VincularAcceso(referencia, reloj ?? throw new InvalidOperationException("Falta el reloj del servidor."));
            return unidad;
        }, ct);
    }
}
