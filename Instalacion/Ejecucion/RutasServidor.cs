using Clarisa.Instalacion.Contratos;

namespace Clarisa.Instalacion.Ejecucion;

internal sealed record RutasServidor(SolicitudServidor Solicitud)
{
    public const string ServicioApi = "Clarisa.Estudios";
    public const string ServicioMotor = "Clarisa.Estudios.MariaDB";
    public const string BaseActiva = "liceo_principal";
    public const string UsuarioAplicacion = "liceo_app";
    public const string UsuarioMigracion = "liceo_migracion";
    public const string UsuarioOperador = "liceo_operador";

    public string Programa => Solicitud.DirectorioPrograma;
    public string Datos => Solicitud.DirectorioDatos;
    public string EjecutableApi => Path.Combine(Programa, "servidor", "Clarisa.Api.exe");
    public string EjecutableMantenimiento => Path.Combine(Programa, "servidor", "Clarisa.Mantenimiento.exe");
    public string EjecutableApiAnterior => Path.Combine(Programa, "servidor", ProgramasAnteriores.Api);
    public string EjecutableMantenimientoAnterior => Path.Combine(Programa, "servidor", ProgramasAnteriores.Mantenimiento);
    // El mantenimiento de la versión instalada, que en las versiones anteriores al cambio de nombre conserva el nombre antiguo.
    public string MantenimientoInstalado =>
        File.Exists(EjecutableMantenimiento) || !File.Exists(EjecutableMantenimientoAnterior) ? EjecutableMantenimiento : EjecutableMantenimientoAnterior;
    public string DirectorioMotor => Path.Combine(Programa, "motor");
    public string EjecutableMotor => Path.Combine(DirectorioMotor, "bin", "mariadbd.exe");
    public string EjecutableInstalarMotor => Path.Combine(DirectorioMotor, "bin", "mariadb-install-db.exe");

    public string Configuracion => Path.Combine(Datos, "configuracion");
    public string ArchivoConfiguracion => Path.Combine(Configuracion, "servidor.json");
    public string Temporal => Path.Combine(Datos, "temporal");
    public string Motor => Path.Combine(Datos, "motor");
    public string DatosMotor => Path.Combine(Motor, "datos");
    public string ArchivoMotorIni => Path.Combine(Motor, "my.ini");
    public string Instalador => Path.Combine(Datos, "instalador");
    public string ArchivoAdministracionMotor => Path.Combine(Instalador, "administracion.protegida");
    // El estado decide qué es propio: vive donde el servicio de la API no puede escribir.
    public string RegistrosInstalacion => Path.Combine(Instalador, "registros");
    public string ArchivoErroresMotor => Path.Combine(Motor, "mariadbd.err");
    public string Certificado => Path.Combine(Datos, "certificado");
    public string ArchivoCertificado => Path.Combine(Certificado, "servidor.pem");
    public string ArchivoClaveCertificado => Path.Combine(Certificado, "servidor-clave.pem");
    public string ArchivoCertificadoPublico => Path.Combine(Certificado, "servidor-estaciones.cer");
    public string Clave => Path.Combine(Datos, "clave");
    public string ArchivoClaveRespaldos => Path.Combine(Clave, "clave-respaldos.json");
    public string Respaldos => Path.Combine(Datos, "respaldos");
    public string Mantenimiento => Path.Combine(Datos, "mantenimiento");
    public string Registros => Path.Combine(Datos, "registros");
    public string ArchivoBaseActiva => Path.Combine(Mantenimiento, "base-activa.json");
    public string ArchivoExclusion => Path.Combine(Mantenimiento, "instancia.bloqueo");

    public Uri DireccionPublica => new($"https://{Anfitrion(Solicitud.NombreConexion)}:{Solicitud.PuertoHttps}/");

    public static string Anfitrion(string nombre) =>
        System.Net.IPAddress.TryParse(nombre, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? $"[{ip}]"
            : nombre;
}
