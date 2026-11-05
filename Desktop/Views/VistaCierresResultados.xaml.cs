using System.Windows;
using System.Windows.Controls;
using Clarisa.Desktop.Presentation.Models;

namespace Clarisa.Desktop.Views;
public partial class VistaCierresResultados : UserControl
{
    public VistaCierresResultados() => InitializeComponent();
    private async void Cargar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloCierresResultados modelo)
            await modelo.IniciarAsync();
    }

    private async void Consultar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloCierresResultados modelo)
            await modelo.ConsultarAsync();
    }

    private async void Cerrar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloCierresResultados modelo)
            await modelo.CerrarAsync();
    }

    private async void Anular(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloCierresResultados modelo)
            await modelo.AnularAsync();
    }

    private void PedirCierre(object sender, RoutedEventArgs e) => (DataContext as ModeloCierresResultados)?.PedirCierre();
    private void CancelarCierre(object sender, RoutedEventArgs e) => (DataContext as ModeloCierresResultados)?.CancelarCierre();
}
