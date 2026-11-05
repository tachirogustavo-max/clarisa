using System.ComponentModel;
using System.Windows;
using Clarisa.Setup.Modelos;
using Clarisa.Setup.Servicios;
using Microsoft.Win32;

namespace Clarisa.Setup;
public partial class VentanaInstalacion : Window
{
    public VentanaInstalacion() => InitializeComponent();
    private ModeloInstalacion Modelo => (ModeloInstalacion)DataContext;
    private async void Continuar(object sender, RoutedEventArgs e) => await Modelo.ContinuarAsync(new OperadorInterfaz(this));
    private void Volver(object sender, RoutedEventArgs e) => Modelo.Volver();
    private void Cancelar(object sender, RoutedEventArgs e) => Close();
    private void Detener(object sender, RoutedEventArgs e) => Modelo.SolicitarCancelacion();
    private void ElegirClave(object sender, RoutedEventArgs e)
    {
        var dialogo = new SaveFileDialog { Title = "Conservar la clave de recuperación", FileName = "Clarisa-clave-recuperacion.txt", Filter = "Clave de recuperación (*.txt)|*.txt", OverwritePrompt = true };
        if (dialogo.ShowDialog(this) == true) Modelo.Configuracion.ExportarClave = dialogo.FileName;
    }
    private void ElegirCertificado(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFileDialog { Title = "Certificado público del servidor", Filter = "Certificado público (*.cer;*.crt;*.pem)|*.cer;*.crt;*.pem", CheckFileExists = true };
        if (dialogo.ShowDialog(this) == true) Modelo.Configuracion.Certificado = dialogo.FileName;
    }
    private void ElegirPrograma(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFolderDialog { Title = "Carpeta para instalar la aplicación", Multiselect = false };
        if (dialogo.ShowDialog(this) == true) Modelo.Configuracion.Programa = dialogo.FolderName;
    }
    private void AbrirRegistro(object sender, RoutedEventArgs e)
    {
        RegistroVisible.Abrir(this, Modelo.ArchivoRegistro);
    }
    private void CerrarVentana(object? sender, CancelEventArgs e)
    {
        if (DataContext is not ModeloInstalacion { Ejecutando: true }) return;
        e.Cancel = true;
        if (new Clarisa.Setup.Vistas.DialogoConfirmacion("¿Detener al terminar el paso actual? La ventana permanecerá abierta hasta que termine de forma segura.") { Owner = this }.ShowDialog() == true) Modelo.SolicitarCancelacion();
    }
    private void AbrirRecuperacion(object sender, RoutedEventArgs e) => App.AbrirRecuperacion(this);
}
