using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Pasos.Servidor;

internal sealed class PasoConfiguracion : IPasoInstalacion
{
    public string Id => "configuracion";
    public string Nombre => "Configuración protegida del servidor";
    public string Descripcion => "Escribe servidor.json con la escucha elegida, el certificado y la credencial de la aplicación; sin credencial de migración.";
    public bool Critico => true;
    public bool Privilegiado => false;

    public Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var rutas = contexto.RutasServidor;
        var actual = ConfiguracionEfectiva.Leer(contexto);
        if (!File.Exists(rutas.ArchivoConfiguracion)) return Task.FromResult(Comprobacion.NoSatisfecha("Se repondrá la copia de configuración desde el entorno protegido, si existe."));
        if (actual is null) return Task.FromResult(Comprobacion.NoSatisfecha("Falta servidor.json."));
        var clave = ConfiguracionServidor.ClaveAplicacion(actual);
        contexto.Filtro.Registrar(clave);
        if (clave is null) return Task.FromResult(Comprobacion.NoSatisfecha("La configuración no tiene credencial de aplicación válida."));
        var diferencia = ConfiguracionServidor.Diferencia(actual,
            ConfiguracionServidor.Obligatoria(rutas, clave, ConfiguracionServidor.BaseConfigurada(actual)));
        return Task.FromResult(diferencia is null
            ? Comprobacion.Satisfecha($"Configuración en {rutas.ArchivoConfiguracion}; escucha {ConfiguracionServidor.UrlEscucha(rutas)}.")
            : Comprobacion.NoSatisfecha(diferencia));
    }

    public Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var rutas = contexto.RutasServidor;
        var actual = ConfiguracionEfectiva.Leer(contexto);
        var clave = ConfiguracionServidor.ClaveAplicacion(actual) ?? CredencialMotor.Generar();
        contexto.Filtro.Registrar(clave);
        var obligatoria = ConfiguracionServidor.Obligatoria(rutas, clave, ConfiguracionServidor.BaseConfigurada(actual));
        ConfiguracionServidor.Escribir(rutas.ArchivoConfiguracion, ConfiguracionServidor.Fusionar(actual, obligatoria));
        contexto.Propio(TipoArtefacto.Archivo, rutas.ArchivoConfiguracion, conservar: true);
        return Task.CompletedTask;
    }
}

internal sealed class PasoMotorCuentas(IMotorDatos motor) : IPasoInstalacion
{
    public string Id => "motor-cuentas";
    public string Nombre => "Base y cuentas mínimas del motor";
    public string Descripcion => "Crea la base, la cuenta de la aplicación (solo datos), la de migración y la del operador de mantenimiento.";
    public bool Critico => true;
    public bool Privilegiado => true;

    public async Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var cuentas = Cuentas(contexto);
        var diferencia = await motor.VerificarCuentasAsync(contexto.Servidor.PuertoMotor, CredencialMotor.Exigir(contexto), cuentas, ct);
        return diferencia is null
            ? Comprobacion.Satisfecha($"Base {cuentas.Base}; la aplicación entra con permisos solo de datos; migración y operador presentes.")
            : Comprobacion.NoSatisfecha(diferencia);
    }

    public async Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        using var operador = await contexto.Operador.SolicitarClaveOperadorMotorAsync(ct)
                             ?? throw new FalloInstalacionException("No se indicó la clave del operador de mantenimiento; no se crearon cuentas.");
        var clave = operador.ClaveTexto();
        contexto.Filtro.Registrar(clave);
        await motor.AsegurarCuentasAsync(contexto.Servidor.PuertoMotor, CredencialMotor.Exigir(contexto), Cuentas(contexto), clave, ct);
        contexto.Propio(TipoArtefacto.BaseDatos, RutasServidor.BaseActiva, conservar: true);
        contexto.Propio(TipoArtefacto.UsuarioMotor, RutasServidor.UsuarioOperador, conservar: true);
    }

    internal static CuentasMotor Cuentas(ContextoEjecucion contexto)
    {
        var configuracion = ConfiguracionEfectiva.Leer(contexto);
        var clave = ConfiguracionServidor.ClaveAplicacion(configuracion)
                    ?? throw new FalloInstalacionException("La configuración no tiene la credencial de la aplicación.");
        contexto.Filtro.Registrar(clave);
        return Cuentas(configuracion, clave);
    }

    internal static CuentasMotor Cuentas(JsonObject? configuracion, string clave) =>
        new(ConfiguracionServidor.BaseConfigurada(configuracion), "liceo\\_%", RutasServidor.UsuarioAplicacion, clave,
            RutasServidor.UsuarioMigracion, RutasServidor.UsuarioOperador);
}

// La migración usa una configuración temporal con la cuenta de migración y una clave nueva que se invalida al terminar, también si falla.
internal sealed class PasoMigracion(IMotorDatos motor) : IPasoInstalacion
{
    public const string Confirmacion = "Esquema de datos actualizado y verificado.";
    private static readonly TimeSpan Limite = TimeSpan.FromMinutes(10);

    public string Id => "migracion";
    public string Nombre => "Esquema de datos";
    public string Descripcion => "Aplica y verifica las migraciones con una credencial separada que no queda en la configuración.";
    public bool Critico => true;
    public bool Privilegiado => true;

    public Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct) => Task.FromResult(
        contexto.Hechos.TryGetValue(Id, out var detalle)
            ? Comprobacion.Satisfecha(detalle)
            : Comprobacion.NoSatisfecha("El esquema se verifica en cada instalación o reparación."));

    public async Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var rutas = contexto.RutasServidor;
        var raiz = CredencialMotor.Exigir(contexto);
        var permanente = ConfiguracionEfectiva.Leer(contexto)
                         ?? throw new FalloInstalacionException("Falta la configuración del servidor.");
        var temporal = Path.Combine(rutas.Temporal, $"migracion-{Guid.NewGuid():N}.json");
        ExceptionDispatchInfo? fallo = null;
        try
        {
            await MigrarAsync(contexto, raiz, permanente, temporal, ct);
        }
        catch (Exception error)
        {
            fallo = ExceptionDispatchInfo.Capture(error);
        }
        var pendientes = await LimpiarAsync(contexto, raiz, temporal);
        if (pendientes is not null) contexto.Anotar(Id, pendientes);
        fallo?.Throw();
        if (pendientes is not null)
        {
            contexto.Hechos.Remove(Id);
            throw new FalloInstalacionException(pendientes + " El esquema quedó confirmado; vuelve a ejecutar el instalador con el mismo " +
                                                "paquete para repetir la verificación y completar la limpieza, sin restaurar datos.");
        }
        ActualizarPunto(rutas, punto => punto with { LimpiezaPendiente = false });
    }

    private static void ActualizarPunto(RutasServidor rutas, Func<PuntoActualizacion, PuntoActualizacion> cambio)
    {
        if (PuntoActualizacion.Leer(rutas) is { Completada: false, ProgramasRevertidos: false } punto) cambio(punto).Guardar(rutas);
    }

    private async Task MigrarAsync(ContextoEjecucion contexto, string raiz, JsonObject permanente, string temporal, CancellationToken ct)
    {
        var rutas = contexto.RutasServidor;
        var clave = CredencialMotor.Generar();
        // Cada intento empieza sin confirmación propia; la de un intento anterior queda solo como EsquemaConfirmado.
        ActualizarPunto(rutas, punto => punto with { MigracionIniciada = true, MigracionConfirmada = false, LimpiezaPendiente = true });
        contexto.Filtro.Registrar(clave);
        await motor.FijarClaveMigracionAsync(contexto.Servidor.PuertoMotor, raiz, RutasServidor.UsuarioMigracion, clave, ct);
        var configuracion = (JsonObject)permanente.DeepClone();
        configuracion["DatosMigracion"] = ConfiguracionServidor.Datos(rutas, ConfiguracionServidor.BaseConfigurada(permanente),
            RutasServidor.UsuarioMigracion, clave);
        ConfiguracionServidor.Escribir(temporal, configuracion);
        var resultado = await contexto.Sistema.Procesos.EjecutarAsync(
            new SolicitudProceso(rutas.EjecutableApi, ["--configuracion", temporal, "--migrar"], Limite)
            { Directorio = Path.GetDirectoryName(rutas.EjecutableApi) }, ct);
        contexto.Anotar(Id, $"Código {resultado.Codigo}. Salida: {resultado.Salida}");
        if (resultado.VencioLimite) throw new FalloInstalacionException("La migración superó su tiempo límite.");
        var confirmada = resultado.Salida.Split('\n').Any(l => string.Equals(l.Trim(), Confirmacion, StringComparison.Ordinal));
        if (resultado.Codigo != 0 || !confirmada)
            throw new FalloInstalacionException($"La migración no confirmó el esquema (código {resultado.Codigo}). El servidor no se inicia con un esquema dudoso.");
        ActualizarPunto(rutas, punto => punto with { MigracionConfirmada = true, EsquemaConfirmado = true });
        contexto.Hechos[Id] = "Migración aplicada y verificador del esquema satisfecho.";
    }

    // Sin cancelación: dejar la credencial temporal válida o en disco es peor que esperar unos segundos más.
    private async Task<string?> LimpiarAsync(ContextoEjecucion contexto, string raiz, string temporal)
    {
        var problemas = new List<string>();
        try
        {
            ConfiguracionServidor.BorrarTemporal(temporal);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            problemas.Add($"No se pudo borrar la configuración temporal de migración ({error.GetType().Name}).");
        }
        try
        {
            await motor.FijarClaveMigracionAsync(contexto.Servidor.PuertoMotor, raiz, RutasServidor.UsuarioMigracion,
                CredencialMotor.Generar(), CancellationToken.None);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            problemas.Add($"No se pudo invalidar la clave temporal de migración ({error.GetType().Name}).");
        }
        return problemas.Count == 0 ? null : string.Join(" ", problemas);
    }
}
