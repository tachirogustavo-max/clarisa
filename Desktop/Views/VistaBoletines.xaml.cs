using System.Windows;
using System.Windows.Controls;
using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Views;
public partial class VistaBoletines : UserControl
{
    public VistaBoletines() => InitializeComponent();
    private async void Cargar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloBoletines modelo)
            await modelo.CargarOpcionesAsync(TipoOpciones.Anios);
    }

    private async void CambiarAnio(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ModeloBoletines modelo)
            await Task.WhenAll(modelo.CargarOpcionesAsync(TipoOpciones.Secciones), modelo.CargarOpcionesAsync(TipoOpciones.Lapsos));
    }

    private async void CambiarSeccion(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ModeloBoletines modelo)
            await modelo.CargarOpcionesAsync(TipoOpciones.Estudiantes);
    }

    private async void GuardarDocumento(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ModeloBoletines modelo)
            return;
        var contenido = await modelo.DescargarPdfAsync();
        if (contenido is null)
            return;
        var mensaje = await Clarisa.Desktop.Presentation.Documents.GuardarPdf.ElegirYGuardarAsync(Window.GetWindow(this), contenido, "Boletin.pdf");
        if (mensaje is not null)
            modelo.MostrarMensaje(mensaje);
    }

    private async void Imprimir(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ModeloBoletines modelo || await modelo.DescargarPdfAsync() is not { } contenido)
            return;
        modelo.MostrarMensaje(await Clarisa.Desktop.Presentation.Documents.AbrirParaImprimir.AbrirAsync(contenido, "Boletin"));
    }

    private async void GuardarSeccion(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ModeloBoletines modelo || await modelo.DescargarPdfAsync(seccionCompleta: true) is not { } contenido)
            return;
        var mensaje = await Clarisa.Desktop.Presentation.Documents.GuardarPdf.ElegirYGuardarAsync(Window.GetWindow(this), contenido, "Boletines-seccion.pdf");
        if (mensaje is not null)
            modelo.MostrarMensaje(mensaje);
    }

    private async void Consultar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloBoletines modelo)
            await modelo.ConsultarAsync();
    }
}
