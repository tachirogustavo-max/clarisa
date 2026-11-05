using System.Text.Json.Nodes;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Pasos.Servidor;

// Si las variables del servicio están dañadas, sin la protección prevista o difieren de servidor.json, la única fuente admitida
// es servidor.json en su carpeta protegida, con una credencial que entra al motor propio y con la confirmación del operador. Todo
// se decide antes de detener la API: si no hay reparación viable, la API sigue como estaba. No se genera otra contraseña SQL.
internal static class ReparacionEntorno
{
    private static readonly TimeSpan LimiteMotor = TimeSpan.FromSeconds(60);

    public static async Task<FuenteConfiguracion> DecidirAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        if (contexto.Fuente is { } decidida) return decidida;
        var rutas = contexto.RutasServidor;
        var archivo = ConfiguracionServidor.Leer(rutas.ArchivoConfiguracion);
        var motivo = ConfiguracionEfectiva.Conflicto(archivo, ConfiguracionEfectiva.LeerEntorno(contexto));
        if (motivo is null) return ConfiguracionEfectiva.Resolver(contexto);
        if (archivo is null)
            throw new FalloInstalacionException(motivo + " No hay servidor.json para repararlo, así que no se detuvo la API ni se cambió nada. " + Salida(rutas));
        await ExigirVerificableAsync(contexto, archivo, motivo, ct);
        var pregunta = motivo + " servidor.json está en su carpeta protegida y su credencial entra al motor propio. ¿Usarlo como única " +
                       "configuración para la copia previa, la actualización y para volver a escribir el entorno del servicio?";
        if (!await contexto.Operador.ConfirmarAsync(pregunta, ct))
            throw new FalloInstalacionException(motivo + " No se autorizó usar servidor.json; no se detuvo la API ni se cambió nada.");
        contexto.Anotar("preparar-actualizacion", "Fuente única: servidor.json verificado y confirmado por el operador. " + motivo);
        return contexto.Fuente = new FuenteConfiguracion(archivo, null, "servidor.json verificado y confirmado");
    }

    private static async Task ExigirVerificableAsync(ContextoEjecucion contexto, JsonObject archivo, string motivo, CancellationToken ct)
    {
        var rutas = contexto.RutasServidor;
        var reglas = PasoDirectorios.Esperadas(rutas).Single(e => e.Ruta == rutas.Configuracion).Reglas;
        if (!contexto.Estado.EsPropio(TipoArtefacto.Directorio, rutas.Configuracion)
            || contexto.Sistema.Permisos.Leer(rutas.Configuracion)?.Coincide(reglas) != true)
            throw new FalloInstalacionException(motivo + " La carpeta de servidor.json no tiene la protección del instalador y no se usa para " +
                                                "reparar; no se detuvo la API ni se cambió nada. " + Salida(rutas));
        var clave = ConfiguracionServidor.ClaveAplicacion(archivo)
                    ?? throw new FalloInstalacionException(motivo + " servidor.json no tiene la credencial de la aplicación. " + Salida(rutas));
        contexto.Filtro.Registrar(clave);
        var motor = ServiciosPropios.ConsultarPropio(contexto, RutasServidor.ServicioMotor)
                    ?? throw new FalloInstalacionException(motivo + " Falta el motor propio para comprobar servidor.json; no se cambió nada.");
        if (!ServiciosPropios.EnEjecucion(motor)) await contexto.Sistema.Servicios.IniciarAsync(RutasServidor.ServicioMotor, LimiteMotor, ct);
        var puerto = ConfiguracionServidor.PuertoConfigurado(archivo) ?? contexto.Servidor.PuertoMotor;
        var diferencia = await contexto.Sistema.Motor.VerificarCuentasAsync(puerto, CredencialMotor.Exigir(contexto),
            PasoMotorCuentas.Cuentas(archivo, clave), ct);
        if (diferencia is not null)
            throw new FalloInstalacionException(motivo + " servidor.json no coincide con el motor propio (" + diferencia + ") y no se usa; no se " +
                                                "detuvo la API ni se cambió nada. " + Salida(rutas));
    }

    private static string Salida(RutasServidor rutas) =>
        $"Salida técnica: copia a {rutas.ArchivoConfiguracion} el servidor.json conservado en el punto de actualización más reciente " +
        $"({Path.Combine(rutas.Instalador, "actualizaciones")}\\<id>\\datos\\configuracion) y vuelve a ejecutar el instalador; se comprobará " +
        "contra el motor antes de usarlo. Los datos y las copias no se modifican.";
}
