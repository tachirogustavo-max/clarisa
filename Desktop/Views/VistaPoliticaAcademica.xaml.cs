using System.Windows;
using System.Windows.Controls;
using Clarisa.Desktop.Presentation.Models;

namespace Clarisa.Desktop.Views;
public partial class VistaPoliticaAcademica : UserControl
{
    public VistaPoliticaAcademica() => InitializeComponent();
    private async void Cargar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloPoliticaAcademica modelo)
            await modelo.IniciarAsync();
    }

    private async void Consultar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloPoliticaAcademica modelo)
            await modelo.ConsultarAsync();
    }

    private async void CrearBorrador(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloPoliticaAcademica modelo)
            await modelo.CrearBorradorAsync();
    }

    private async void Guardar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloPoliticaAcademica modelo)
            await modelo.GuardarBorradorAsync();
    }

    private async void Activar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloPoliticaAcademica modelo)
            await modelo.ActivarAsync();
    }

    private void PedirActivacion(object sender, RoutedEventArgs e) => (DataContext as ModeloPoliticaAcademica)?.PedirActivacion();
    private void CancelarActivacion(object sender, RoutedEventArgs e) => (DataContext as ModeloPoliticaAcademica)?.CancelarActivacion();
}
