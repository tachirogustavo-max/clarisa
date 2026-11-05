using System.Windows;
using System.Windows.Controls;
using Clarisa.Desktop.Presentation.Models;

namespace Clarisa.Desktop.Views;
public partial class VistaFirmantes : UserControl
{
    public VistaFirmantes() => InitializeComponent();
    private async void Cargar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloFirmantes { HayCambios: false } modelo)
            await modelo.ConsultarAsync();
    }

    private async void Guardar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloFirmantes modelo)
            await modelo.GuardarAsync();
    }

    private async void Descartar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloFirmantes modelo)
            await modelo.DescartarAsync();
    }
}
