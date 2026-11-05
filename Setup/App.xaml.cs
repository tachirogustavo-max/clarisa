using System.Windows;
using Clarisa.Setup.Modelos;
using Clarisa.Instalacion;
using Clarisa.Instalacion.Contratos;

namespace Clarisa.Setup;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (AbrirRetirada(e.Args)) return;
        if (e.Args.Length == 1 && e.Args[0] == OpcionRecuperar) { AbrirRecuperacion(); return; }
        var modelo = new ModeloInstalacion();
        var ventana = new VentanaInstalacion { DataContext = modelo };
        MainWindow = ventana;
        ventana.Show();
    }

    public const string OpcionRecuperar = "--recuperar";

    // También se abre desde el botón «Recuperar una copia» de la ventana de instalación.
    public static void AbrirRecuperacion(Window? propietario = null)
    {
        var motor = ComposicionInstalacion.CrearRecuperacion();
        var ventana = new VentanaRecuperacion(new ModeloRecuperacion(motor.CarpetaCopias(), motor.RevisarAsync, motor.RecuperarAsync,
            motor.VolverAnteriorAsync)) { Owner = propietario };
        if (propietario is null) Current.MainWindow = ventana;
        ventana.Show();
    }

    private bool AbrirRetirada(string[] argumentos)
    {
        SolicitudRetirada? solicitud;
        string? problema;
        try { solicitud = ArgumentosRetirada.Interpretar(argumentos, out problema); }
        catch (Exception error) when (error is ArgumentException or System.IO.IOException or NotSupportedException)
        {
            solicitud = null;
            problema = "La referencia de retirada no tiene una ruta válida. Utiliza la entrada registrada de Clarisa en Windows.";
        }
        if (solicitud is null && problema is null) return false;
        ModeloRetirada modelo;
        if (problema is not null) modelo = new ModeloRetirada(problema);
        else
        {
            var motor = ComposicionInstalacion.CrearRetirada();
            modelo = new ModeloRetirada(motor.RevisarAsync,
                (revision, progreso, ct) => motor.EjecutarAsync(revision, true, progreso, ct));
        }
        var ventana = new VentanaRetirada(modelo, solicitud);
        MainWindow = ventana;
        ventana.Show();
        return true;
    }
}
