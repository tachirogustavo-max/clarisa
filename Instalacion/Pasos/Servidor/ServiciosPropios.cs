using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Pasos.Servidor;

// Un servicio con el nombre previsto solo se detiene o modifica si esta instalación lo registró o si ya ejecuta el programa de
// esta misma carpeta. Coincidir en el nombre no basta.
internal static class ServiciosPropios
{
    // La API de una versión anterior al cambio de nombre ejecuta Escolar.Api.exe en la misma carpeta: también es propia.
    public static string[] EjecutablesEsperados(RutasServidor rutas, string servicio) =>
        servicio == RutasServidor.ServicioMotor ? [rutas.EjecutableMotor] : [rutas.EjecutableApi, rutas.EjecutableApiAnterior];

    public static string? Ajeno(ContextoEjecucion contexto, InfoServicio? actual)
    {
        if (actual is null || contexto.Estado.EsPropio(TipoArtefacto.Servicio, actual.Nombre)) return null;
        return ProgramasAnteriores.MismoArchivo(actual.Ejecutable, EjecutablesEsperados(contexto.RutasServidor, actual.Nombre))
            ? null
            : $"Ya existe un servicio {actual.Nombre} que ejecuta otro programa y no pertenece a esta instalación. No se detiene ni se modifica; " +
              "retírelo o revíselo con el responsable técnico.";
    }

    public static InfoServicio? ConsultarPropio(ContextoEjecucion contexto, string nombre)
    {
        var actual = contexto.Sistema.Servicios.Consultar(nombre);
        if (Ajeno(contexto, actual) is { } motivo) throw new FalloInstalacionException(motivo);
        return actual;
    }

    public static bool EnEjecucion(InfoServicio? servicio) => servicio?.Estado is EstadoServicio.EnEjecucion or EstadoServicio.Iniciando;
}
