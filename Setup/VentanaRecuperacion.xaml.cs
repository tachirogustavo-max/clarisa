using System.ComponentModel;
using System.Windows;
using Clarisa.Setup.Modelos;
using Clarisa.Setup.Vistas;
using Microsoft.Win32;

namespace Clarisa.Setup;

public partial class VentanaRecuperacion : Window
{
    public VentanaRecuperacion() => InitializeComponent();

    public VentanaRecuperacion(ModeloRecuperacion modelo) : this() => DataContext = modelo;

    private ModeloRecuperacion? Modelo => DataContext as ModeloRecuperacion;

    private void ElegirCopia(object sender, RoutedEventArgs e)
    {
        if (Modelo is not { } modelo) return;
        var dialogo = new OpenFileDialog { Title = "Copia que quieres recuperar", Filter = "Copia de seguridad (*.liceo-respaldo)|*.liceo-respaldo" };
        if (modelo.CarpetaCopias is { } carpeta && System.IO.Directory.Exists(carpeta)) dialogo.InitialDirectory = carpeta;
        if (dialogo.ShowDialog(this) == true) modelo.Copia = dialogo.FileName;
    }

    private void ElegirClave(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFileDialog { Title = "Archivo de recuperación de la clave", Filter = "Archivo de recuperación (*.txt)|*.txt|Todos los archivos|*.*" };
        if (Modelo is { } modelo && dialogo.ShowDialog(this) == true) modelo.ClaveRecuperacion = dialogo.FileName;
    }

    private async void Comprobar(object sender, RoutedEventArgs e)
    {
        if (Modelo is { } modelo) await modelo.ComprobarAsync();
    }

    private async void Recuperar(object sender, RoutedEventArgs e)
    {
        if (Modelo is not { PuedeRecuperar: true } modelo) return;
        var confirmacion = new DialogoConfirmacion("El servidor se detendrá y volverá a iniciarse con la información de la copia elegida. La información " +
                                                   "actual se conserva. ¿Quieres continuar?") { Owner = this, Title = "Confirmar recuperación" };
        if (confirmacion.ShowDialog() != true) return;
        await modelo.RecuperarAsync(Contrasena.Password);
        Contrasena.Clear();
    }

    private async void Volver(object sender, RoutedEventArgs e)
    {
        if (Modelo is not { PuedeVolver: true } modelo) return;
        var confirmacion = new DialogoConfirmacion("El servidor volverá a la información que tenía antes de la última recuperación. ¿Quieres continuar?")
            { Owner = this, Title = "Volver a la información anterior" };
        if (confirmacion.ShowDialog() != true) return;
        await modelo.VolverAsync(Contrasena.Password);
        Contrasena.Clear();
    }

    private void Cerrar(object sender, RoutedEventArgs e) => Close();

    // Mientras se recupera no se cierra la ventana: el servidor podría quedar detenido sin que nadie lo vea.
    private void CerrarVentana(object? sender, CancelEventArgs e)
    {
        if (Modelo is { Ocupado: true }) e.Cancel = true;
    }
}
