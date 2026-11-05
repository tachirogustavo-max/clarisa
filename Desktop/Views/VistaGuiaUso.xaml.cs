using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace Clarisa.Desktop.Views;
public partial class VistaGuiaUso : UserControl
{
    public event EventHandler? CierreSolicitado;
    public VistaGuiaUso() { InitializeComponent(); Loaded += (_, _) => Entendido.Focus(); }
    private void Cerrar(object sender, RoutedEventArgs e) => CierreSolicitado?.Invoke(this, EventArgs.Empty);
    private void Tecla(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Cerrar(sender, e);
        e.Handled = true;
    }
}
