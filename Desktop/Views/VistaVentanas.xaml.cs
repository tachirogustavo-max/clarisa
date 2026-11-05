using System.Windows;
using System.Windows.Controls;
using Clarisa.Desktop.Presentation.Models;

namespace Clarisa.Desktop.Views;
public partial class VistaVentanas : UserControl
{
    public VistaVentanas() => InitializeComponent();
    private async void Cargar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloVentanas modelo)
            await modelo.IniciarAsync();
    }

    private async void Consultar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloVentanas modelo)
            await modelo.ConsultarAsync();
    }

    private void PedirActual(object sender, RoutedEventArgs e) => (DataContext as ModeloVentanas)?.PedirActual();
    private void CancelarActual(object sender, RoutedEventArgs e) => (DataContext as ModeloVentanas)?.CancelarActual();
    private async void ConfirmarActual(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloVentanas modelo)
            await modelo.ConfirmarActualAsync();
    }
}
