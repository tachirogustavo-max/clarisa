using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Pasos.Servidor;

internal sealed class PasoMotorInicial(IMotorDatos motor) : IPasoInstalacion
{
    public string Id => "motor-inicial";
    public string Nombre => "Datos iniciales del motor";
    public string Descripcion => "Crea el directorio de datos del motor propio y protege su clave de administración. Nunca reinicializa datos existentes.";
    public bool Critico => true;
    public bool Privilegiado => true;

    public Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var rutas = contexto.RutasServidor;
        var inicializado = Directory.Exists(Path.Combine(rutas.DatosMotor, "mysql"));
        var credencial = CredencialMotor.Leer(contexto) is not null;
        return Task.FromResult((inicializado, credencial) switch
        {
            (true, true) => Comprobacion.Satisfecha("Datos del motor presentes y credencial de administración legible."),
            (true, false) when File.Exists(Marca(rutas)) => Comprobacion.NoSatisfecha("Una inicialización anterior quedó interrumpida; se rehace."),
            (true, false) => Comprobacion.NoSatisfecha("Hay datos del motor sin credencial de este instalador. No se reinicializan."),
            _ => Comprobacion.NoSatisfecha("El motor todavía no tiene datos.")
        });
    }

    // La marca, en la carpeta solo de administración, distingue una inicialización propia interrumpida (se puede rehacer) de datos ajenos.
    public async Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var rutas = contexto.RutasServidor;
        RutasSeguras.ExigirSinPuntosReparacion(rutas.DatosMotor);
        if (File.Exists(Marca(rutas)) && !File.Exists(rutas.ArchivoAdministracionMotor)) Descartar(contexto);
        if (Directory.Exists(rutas.DatosMotor) && Directory.EnumerateFileSystemEntries(rutas.DatosMotor).Any())
            throw new FalloInstalacionException($"La carpeta {rutas.DatosMotor} ya contiene datos ajenos o incompletos. No se borra ni se " +
                                                "reinicializa: revísala manualmente o elige otra carpeta de datos.");
        if (File.Exists(rutas.ArchivoAdministracionMotor))
            throw new FalloInstalacionException("Existe una credencial de motor sin datos; revisa la instalación anterior antes de continuar.");
        var clave = CredencialMotor.Generar();
        contexto.Filtro.Registrar(clave);
        EscrituraSegura.ReemplazarTexto(Marca(rutas), contexto.Sistema.Reloj().ToString("O"));
        try
        {
            await motor.InicializarAsync(rutas.EjecutableInstalarMotor, rutas.EjecutableMotor, rutas.DatosMotor, clave, ct);
        }
        catch
        {
            DescartarSiPuede(contexto);
            throw;
        }
        CredencialMotor.Guardar(contexto, clave);
        File.Delete(Marca(rutas));
        contexto.Propio(TipoArtefacto.Directorio, rutas.DatosMotor, conservar: true);
    }

    private static string Marca(RutasServidor rutas) => Path.Combine(rutas.Instalador, "motor-inicializando.marca");

    // Nunca se descarta una carpeta con bases del liceo: esas solo existen después de una inicialización completa.
    private static void Descartar(ContextoEjecucion contexto)
    {
        var rutas = contexto.RutasServidor;
        if (Directory.Exists(rutas.DatosMotor))
        {
            if (Directory.EnumerateDirectories(rutas.DatosMotor, "liceo*").Any())
                throw new FalloInstalacionException($"La carpeta {rutas.DatosMotor} contiene bases del liceo sin credencial; no se borra.");
            Directory.Delete(rutas.DatosMotor, recursive: true);
        }
        File.Delete(Marca(rutas));
        contexto.Anotar("motor-inicial", "Se descartaron los datos de una inicialización propia interrumpida.");
    }

    private static void DescartarSiPuede(ContextoEjecucion contexto)
    {
        try
        {
            Descartar(contexto);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FalloInstalacionException)
        {
            contexto.Anotar("motor-inicial", $"Los datos parciales quedan para la próxima ejecución ({error.GetType().Name}).");
        }
    }
}

internal sealed class PasoMotorServicio(IMotorDatos motor) : IPasoInstalacion
{
    private static readonly TimeSpan LimiteArranque = TimeSpan.FromSeconds(90);

    public string Id => "motor-servicio";
    public string Nombre => "Servicio del motor de datos";
    public string Descripcion => "Registra el motor propio con cuenta virtual, solo en 127.0.0.1, y comprueba una consulta real a su puerto y datos.";
    public bool Critico => true;
    public bool Privilegiado => true;

    public async Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var rutas = contexto.RutasServidor;
        if (!ConfiguracionMotor.Vigente(rutas)) return Comprobacion.NoSatisfecha("La configuración del motor falta o fue modificada.");
        var servicio = contexto.Sistema.Servicios.Consultar(RutasServidor.ServicioMotor);
        if (ServiciosPropios.Ajeno(contexto, servicio) is { } ajeno) return Comprobacion.NoSatisfecha(ajeno);
        if (ComparadorServicio.Diferencia(servicio, Definicion(rutas)) is { } diferencia) return Comprobacion.NoSatisfecha(diferencia);
        var identidad = await motor.IdentificarAsync(contexto.Servidor.PuertoMotor, CredencialMotor.Exigir(contexto), ct);
        if (identidad is null) return Comprobacion.NoSatisfecha($"No responde el motor propio en 127.0.0.1:{contexto.Servidor.PuertoMotor}.");
        if (identidad.Puerto != contexto.Servidor.PuertoMotor
            || !Path.TrimEndingDirectorySeparator(Path.GetFullPath(identidad.DirectorioDatos))
                .Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(rutas.DatosMotor)), StringComparison.OrdinalIgnoreCase))
            return Comprobacion.NoSatisfecha("El puerto responde, pero no es el motor de esta instalación.");
        if (identidad.Direccion != "127.0.0.1") return Comprobacion.NoSatisfecha($"El motor escucha en {identidad.Direccion}, no solo en 127.0.0.1.");
        return Comprobacion.Satisfecha($"MariaDB {identidad.Version} en 127.0.0.1:{identidad.Puerto} con los datos de esta instalación.");
    }

    public async Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var rutas = contexto.RutasServidor;
        var servicios = contexto.Sistema.Servicios;
        if (ServiciosPropios.EnEjecucion(ServiciosPropios.ConsultarPropio(contexto, RutasServidor.ServicioMotor)))
            await servicios.DetenerAsync(RutasServidor.ServicioMotor, LimiteArranque, ct);
        EscrituraSegura.ReemplazarTexto(rutas.ArchivoMotorIni, ConfiguracionMotor.Texto(rutas));
        contexto.Propio(TipoArtefacto.Archivo, rutas.ArchivoMotorIni);
        servicios.CrearOActualizar(Definicion(rutas));
        contexto.Propio(TipoArtefacto.Servicio, RutasServidor.ServicioMotor);
        await servicios.IniciarAsync(RutasServidor.ServicioMotor, LimiteArranque, ct);
    }

    internal static DefinicionServicio Definicion(RutasServidor rutas) => new(RutasServidor.ServicioMotor,
        NombreSistema.Visible + " - Motor de datos", "Motor MariaDB propio del servidor del liceo; solo acepta conexiones locales.",
        rutas.EjecutableMotor, ["--defaults-file=" + rutas.ArchivoMotorIni, RutasServidor.ServicioMotor],
        IdentidadServicio.Cuenta(RutasServidor.ServicioMotor), []);
}

internal static class ConfiguracionMotor
{
    public static bool Vigente(RutasServidor rutas)
    {
        var info = new FileInfo(rutas.ArchivoMotorIni);
        return info is { Exists: true, Length: < 64 * 1024 } && File.ReadAllText(info.FullName) == Texto(rutas);
    }

    public static string Texto(RutasServidor rutas) => $"""
        [mysqld]
        datadir={rutas.DatosMotor.Replace('\\', '/')}
        port={rutas.Solicitud.PuertoMotor}
        bind-address=127.0.0.1
        skip-name-resolve
        local-infile=0
        skip-log-bin
        character-set-server=utf8mb4
        collation-server=utf8mb4_unicode_520_ci
        max-connections=80
        log-error={rutas.ArchivoErroresMotor.Replace('\\', '/')}

        """;
}

internal static class ComparadorServicio
{
    public static string? Diferencia(InfoServicio? actual, DefinicionServicio esperado)
    {
        if (actual is null) return $"El servicio {esperado.Nombre} no está registrado.";
        if (!string.Equals(actual.LineaComando, esperado.LineaComando, StringComparison.OrdinalIgnoreCase))
            return $"El servicio {esperado.Nombre} tiene otra ruta o argumentos.";
        if (!string.Equals(actual.Cuenta, esperado.Cuenta, StringComparison.OrdinalIgnoreCase))
            return $"El servicio {esperado.Nombre} usa otra cuenta ({actual.Cuenta}).";
        if (!actual.InicioAutomatico) return $"El servicio {esperado.Nombre} no inicia automáticamente.";
        if (actual.Visible is not null && !string.Equals(actual.Visible, esperado.Visible, StringComparison.Ordinal))
            return $"El servicio {esperado.Nombre} se muestra con otro nombre ({actual.Visible}).";
        return actual.Estado == EstadoServicio.EnEjecucion ? null : $"El servicio {esperado.Nombre} no está en ejecución ({actual.Estado}).";
    }
}
