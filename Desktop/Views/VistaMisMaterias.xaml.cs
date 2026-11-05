using System.Windows;
using System.Windows.Controls;
using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Views;
public partial class VistaMisMaterias : UserControl
{
    public VistaMisMaterias() => InitializeComponent();
    private async void Cargar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloMisMaterias modelo)
            await modelo.IniciarAsync();
    }

    private async void CambiarAnio(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ModeloMisMaterias modelo)
            await modelo.CambiarAnioAsync();
    }
    private async void Reconsultar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloMisMaterias modelo && !modelo.Abriendo) await modelo.BuscarAsync();
    }

    private async void Abrir(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AsignacionRemota asignacion } && DataContext is ModeloMisMaterias modelo)
            await modelo.AbrirAsync(asignacion);
    }
}
