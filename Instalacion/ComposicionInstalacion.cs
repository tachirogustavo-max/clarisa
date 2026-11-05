using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Planificacion;
using Clarisa.Instalacion.Sistema;
using Clarisa.Instalacion.Sistema.Windows;

namespace Clarisa.Instalacion;

// Único punto que une el motor con los efectos reales de Windows. La interfaz no instancia adaptadores por su cuenta.
public static class ComposicionInstalacion
{
    // Carpeta distinta de la de datos del servidor: un mismo equipo puede tener ambos modos sin mezclar permisos ni estados.
    public static string RegistrosEstacion => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Clarisa Estudios Estacion", "registros");

    public static PlanificadorInstalacion CrearPlanificador() => CrearPlanificador(SistemaReal(), RegistrosEstacion);

    public static MotorInstalacion CrearMotor(string huellaManifiestoEsperada)
    {
        ArgumentNullException.ThrowIfNull(huellaManifiestoEsperada);
        if (huellaManifiestoEsperada.Length != 64 || !huellaManifiestoEsperada.All(Uri.IsHexDigit))
            throw new ArgumentException("La huella del paquete no es válida.", nameof(huellaManifiestoEsperada));
        var sistema = SistemaReal();
        return new(sistema, modo => CatalogoPasos.Para(modo, sistema), RegistrosEstacion, huellaManifiestoEsperada);
    }

    // Modo --desinstalar de Setup: revisa y ejecuta la retirada a partir de la referencia protegida (ver ArgumentosRetirada).
    public static Retirada.MotorRetirada CrearRetirada() => new(SistemaReal(), RegistrosEstacion);

    internal static Retirada.MotorRetirada CrearRetirada(SistemaInstalacion sistema, string registrosEstacion) => new(sistema, registrosEstacion);

    // Recuperación guiada de una copia de seguridad en el servidor instalado (ventana «Recuperar una copia» del instalador).
    public static Recuperacion.MotorRecuperacion CrearRecuperacion() => new(SistemaReal());

    internal static Recuperacion.MotorRecuperacion CrearRecuperacion(SistemaInstalacion sistema) => new(sistema);

    internal static PlanificadorInstalacion CrearPlanificador(SistemaInstalacion sistema, string registrosEstacion) =>
        new(sistema.Equipo, modo => CatalogoPasos.Para(modo, sistema), registrosEstacion);

    internal static MotorInstalacion CrearMotor(SistemaInstalacion sistema, string registrosEstacion) =>
        new(sistema, modo => CatalogoPasos.Para(modo, sistema), registrosEstacion);

    internal static SistemaInstalacion SistemaReal()
    {
        var procesos = new ProcesosSistema();
        return new SistemaInstalacion(procesos, new ServiciosWindows(), new PermisosNtfs(), new AlmacenConfianzaEquipo(),
            new FirewallWindows(), new ProteccionDpapiEquipo(), new EquipoWindows(), new AccesosDirectosShell(),
            new ClienteServidorHttps(), new MotorDatosMariaDb(procesos), () => DateTimeOffset.Now, Task.Delay, new RegistroAplicacionesWindows())
        {
            // Solo la distribución de archivo único funciona copiada sola; desde una compilación de carpeta no se registra la retirada.
            Herramienta = HerramientaPublicada()
        };
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("SingleFile", "IL3000",
        Justification = "La ubicación vacía identifica deliberadamente la biblioteca integrada en el ejecutable único.")]
    private static string? HerramientaPublicada() =>
        string.IsNullOrEmpty(typeof(ComposicionInstalacion).Assembly.Location) ? Environment.ProcessPath : null;
}
