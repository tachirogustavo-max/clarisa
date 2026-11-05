using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Clarisa.Desktop.Presentation.Models;

namespace Clarisa.Desktop.Views;
public partial class VistaReportes : UserControl
{
    public VistaReportes() => InitializeComponent();
    private async void Cargar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloReportes modelo)
            await modelo.CargarAsync();
    }

    private async void Consultar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloReportes modelo)
            await modelo.BuscarAsync();
    }

    private async void Limpiar(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ModeloReportes modelo) return;
        modelo.LimpiarFiltros();
        await modelo.BuscarAsync();
    }
    private async void Exportar(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ModeloReportes modelo) return;
        var contenido = await modelo.DescargarPdfAsync();
        if (contenido is null) return;
        var mensaje = await Clarisa.Desktop.Presentation.Documents.GuardarPdf.ElegirYGuardarAsync(Window.GetWindow(this), contenido, "Calificaciones.pdf");
        if (mensaje is not null) modelo.MostrarMensaje(mensaje);
    }
    private void TeclasBusqueda(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        Consultar(sender, e);
        e.Handled = true;
    }
}
