using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Clarisa.Desktop.Presentation.Models;

namespace Clarisa.Desktop.Views;
public partial class VistaClavePropia : UserControl
{
    public event EventHandler? CierreSolicitado;
    public event EventHandler<string>? SalidaSolicitada;
    public VistaClavePropia() { InitializeComponent(); Loaded += (_, _) => Actual.Focus(); }
    public void LimpiarCampos() { Actual.Limpiar(); Nueva.Limpiar(); Confirmacion.Limpiar(); }
    private async void Guardar(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ModeloClavePropia modelo) return;
        using var actual = Actual.ObtenerClave();
        using var nueva = Nueva.ObtenerClave();
        using var confirmacion = Confirmacion.ObtenerClave();
        var resultado = await modelo.CambiarAsync(actual, nueva, confirmacion);
        if (resultado?.Salir == true)
        {
            Actual.Limpiar(); Nueva.Limpiar(); Confirmacion.Limpiar();
            SalidaSolicitada?.Invoke(this, resultado.Mensaje);
        }
        else if (modelo.ErrorActual.Length > 0) Actual.Focus();
        else if (modelo.ErrorNueva.Length > 0) Nueva.Focus();
        else if (modelo.ErrorConfirmacion.Length > 0) Confirmacion.Focus();
    }
    private void Cerrar(object sender, RoutedEventArgs e) { if (DataContext is ModeloClavePropia { Disponible: true }) CierreSolicitado?.Invoke(this, EventArgs.Empty); }
    private void Tecla(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Cerrar(sender, e); e.Handled = true; }
    }
}
