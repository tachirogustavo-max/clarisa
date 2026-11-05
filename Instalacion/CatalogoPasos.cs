using Clarisa.Instalacion.Contratos;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Pasos.Comunes;
using Clarisa.Instalacion.Pasos.Estacion;
using Clarisa.Instalacion.Pasos.Servidor;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion;

internal static class CatalogoPasos
{
    public static IReadOnlyList<IPasoInstalacion> Para(ModoInstalacion modo, SistemaInstalacion sistema) => modo switch
    {
        ModoInstalacion.Servidor =>
        [
            new PasoRequisitos(),
            new PasoPuertos(),
            new PasoPaquete(),
            new PasoPrepararActualizacion(),
            new PasoDetenerServicios(),
            new PasoPrograma(),
            new PasoDirectorios(),
            new PasoMotorInicial(sistema.Motor),
            new PasoMotorServicio(sistema.Motor),
            new PasoCertificado(),
            new PasoConfiguracion(),
            new PasoMotorCuentas(sistema.Motor),
            new PasoMigracion(sistema.Motor),
            new PasoPrimeraCuenta(),
            new PasoClaveRespaldos(),
            new PasoServicioApi(),
            new PasoRegistroRetirada(),
            new PasoRedLocal(),
            new PasoVerificacionApi(),
            new PasoReinicioServicio(),
            new PasoCopiasAutomaticas(),
            new PasoProgramasAnteriores()
        ],
        ModoInstalacion.Estacion =>
        [
            new PasoRequisitos(),
            new PasoPaquete(),
            new PasoPrograma(),
            new PasoRegistroRetirada(),
            new PasoConfianzaCertificado(),
            new PasoConexionServidor(),
            new PasoAccesoDirecto(),
            new PasoProgramasAnteriores()
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(modo))
    };
}
