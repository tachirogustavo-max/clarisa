using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Clarisa.Desktop.Presentation.Models;
namespace Clarisa.Desktop.Controls;
public partial class DialogoExoneracion : UserControl
{
    public DialogoExoneracion()
    {
        InitializeComponent();
        IsVisibleChanged += (_, e) => { if (e.NewValue is true) MoveFocus(new TraversalRequest(FocusNavigationDirection.First)); };
    }
    private void Teclas(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        (DataContext as ModeloExoneracion)?.Cerrar();
        e.Handled = true;
    }
    public void OrientarSalidaBloqueada()
    {
        (DataContext as ModeloExoneracion)?.AvisarSalidaBloqueada();
        if (!IsKeyboardFocusWithin)
            MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
    }
    private void Cerrar(object sender, RoutedEventArgs e) => (DataContext as ModeloExoneracion)?.Cerrar();
    private async void Confirmar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloExoneracion modelo) await modelo.GuardarAsync();
    }
}
