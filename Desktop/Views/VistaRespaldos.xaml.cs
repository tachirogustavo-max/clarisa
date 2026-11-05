using System.Windows;
using System.Windows.Controls;
using Clarisa.Desktop.Presentation.Models;
using Microsoft.Win32;

namespace Clarisa.Desktop.Views;
public partial class VistaRespaldos : UserControl
{
    public VistaRespaldos() => InitializeComponent();
    private async void Cargar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloRespaldos modelo)
            await modelo.CargarAsync();
    }

    private async void Crear(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloRespaldos modelo)
            await modelo.CrearAsync();
    }

    private async void Comprobar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloRespaldos modelo)
            await modelo.ComprobarAsync();
    }

    private async void Descargar(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ModeloRespaldos { Descargando: false } modelo) return;
        if (modelo.Seleccionado is not { Verificado: true } copia)
        {
            await modelo.DescargarAsync("");
            return;
        }
        var dialogo = new SaveFileDialog
        {
            Filter = "Copia cifrada del liceo (*.liceo-respaldo)|*.liceo-respaldo",
            DefaultExt = ".liceo-respaldo", AddExtension = true, OverwritePrompt = true,
            FileName = "respaldo-" + copia.CreadoEn.ToLocalTime().ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture) + "-" + copia.Id.ToString("N")[..8] + ".liceo-respaldo"
        };
        if (dialogo.ShowDialog(Window.GetWindow(this)) == true) await modelo.DescargarAsync(dialogo.FileName);
    }

    private async void PedirRestauracion(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloRespaldos modelo) await modelo.PedirRestauracionAsync();
    }
    private void Cancelar(object sender, RoutedEventArgs e)
    {
        (DataContext as ModeloRespaldos)?.CerrarGuia();
        BotonRecuperacion.Focus();
    }
    private void TeclaGuia(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape) return;
        Cancelar(sender, e);
        e.Handled = true;
    }
}
