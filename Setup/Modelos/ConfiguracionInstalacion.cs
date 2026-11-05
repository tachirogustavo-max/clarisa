using System.IO;
using System.Net;
using Clarisa.Instalacion.Contratos;
using Clarisa.Setup.Servicios;

namespace Clarisa.Setup.Modelos;
public sealed class ConfiguracionInstalacion : ModeloObservable
{
    private bool servidor = true;
    private static readonly string ProgramaPredeterminado = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Clarisa Estudios");
    // En la computadora del servidor la estación no puede compartir su carpeta: usa una propia al lado.
    private static readonly string ProgramaEstacionJuntoAlServidor = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Clarisa Estudios Estacion");
    private string programa = ProgramaPredeterminado;
    private string datos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Clarisa Estudios");
    private string nombreConexion = Environment.MachineName;
    private string direccionEscucha = "";
    private string direccionServidor = "";
    private string certificado = "";
    private string huella = "";
    private string exportarClave = "";
    private string usuario = "direccion";
    private RedInstalacion? red;
    public IReadOnlyList<RedInstalacion> Redes { get; private set; } = [];
    public RedInstalacion? Red
    {
        get => red;
        set
        {
            Cambiar(ref red, value);
            if (value is not null) DireccionEscucha = value.Direccion;
            Notificar(nameof(AvisoRed));
        }
    }
    // El servidor escucha solo en esta dirección: si el router se la cambia al reiniciar, deja de atender a las estaciones.
    public string AvisoRed => red is { Automatica: true }
        ? "Esta dirección la asigna el router y puede cambiar al reiniciar el equipo; si cambia, el sistema deja de funcionar. Antes de instalar, déjala fija (guía del servidor, «Dirección fija del servidor»)."
        : "";
    public void SugerirRed()
    {
        if (!Servidor || !string.IsNullOrWhiteSpace(DireccionEscucha)) return;
        EstablecerRedes(RedesInstalacion.Detectar());
    }
    internal void EstablecerRedes(IReadOnlyList<RedInstalacion> redes)
    {
        Redes = redes;
        Notificar(nameof(Redes));
        if (Redes.Count == 1) Red = Redes[0];
    }
    public string OrigenPaquete { get; set; } = Path.Combine(AppContext.BaseDirectory, "paquete");
    public bool Servidor
    {
        get => servidor;
        set
        {
            if (servidor == value) return;
            Cambiar(ref servidor, value);
            Notificar(nameof(Estacion));
            if (!value && programa == ProgramaPredeterminado && Directory.Exists(Path.Combine(ProgramaPredeterminado, "servidor")))
                Programa = ProgramaEstacionJuntoAlServidor;
            else if (value && programa == ProgramaEstacionJuntoAlServidor)
                Programa = ProgramaPredeterminado;
        }
    }
    public bool Estacion { get => !servidor; set => Servidor = !value; }
    public string Programa { get => programa; set => Cambiar(ref programa, value); }
    public string Datos { get => datos; set => Cambiar(ref datos, value); }
    public string NombreConexion { get => nombreConexion; set => Cambiar(ref nombreConexion, value); }
    public string DireccionEscucha { get => direccionEscucha; set => Cambiar(ref direccionEscucha, value); }
    public string DireccionServidor { get => direccionServidor; set => Cambiar(ref direccionServidor, value); }
    public string Certificado { get => certificado; set => Cambiar(ref certificado, value); }
    public string Huella { get => huella; set => Cambiar(ref huella, value); }
    public string ExportarClave { get => exportarClave; set => Cambiar(ref exportarClave, value); }
    public string Usuario { get => usuario; set => Cambiar(ref usuario, value); }
    private string puertoHttps = "5189";
    private string puertoMotor = "43306";
    public string TextoPuertoHttps { get => puertoHttps; set => Cambiar(ref puertoHttps, value ?? ""); }
    public string TextoPuertoMotor { get => puertoMotor; set => Cambiar(ref puertoMotor, value ?? ""); }
    public int PuertoHttps { get => int.TryParse(puertoHttps, out var puerto) ? puerto : 0; set => TextoPuertoHttps = value.ToString(System.Globalization.CultureInfo.InvariantCulture); }
    public int PuertoMotor { get => int.TryParse(puertoMotor, out var puerto) ? puerto : 0; set => TextoPuertoMotor = value.ToString(System.Globalization.CultureInfo.InvariantCulture); }
    public SolicitudInstalacion CrearSolicitud()
    {
        if (!Servidor) return new SolicitudEstacion(OrigenPaquete, Programa, DireccionServidor, Certificado, Huella);
        if (!IPAddress.TryParse(DireccionEscucha, out var direccion)) throw new ArgumentException("Selecciona la red del liceo. Si no aparece, revisa los ajustes avanzados.");
        if (PuertoHttps is < 1 or > 65535 || PuertoMotor is < 1 or > 65535)
            throw new ArgumentException("Revisa los puertos en ajustes avanzados: deben ser números entre 1 y 65535.");
        return new SolicitudServidor(OrigenPaquete, Programa, Datos, NombreConexion, direccion, PuertoHttps, PuertoMotor, ExportarClave, Usuario);
    }
}
