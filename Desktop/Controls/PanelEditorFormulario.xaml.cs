using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Clarisa.Desktop.Presentation.Models;

namespace Clarisa.Desktop.Controls;
public partial class PanelEditorFormulario : ContentControl
{
    public PanelEditorFormulario() => InitializeComponent();
    private void Cerrar(object sender, RoutedEventArgs e) => (DataContext as IEditorCatalogo)?.CerrarEditor();
    private void Seguir(object sender, RoutedEventArgs e) => (DataContext as IEditorCatalogo)?.SeguirEditando();
    private void Descartar(object sender, RoutedEventArgs e) => (DataContext as IEditorCatalogo)?.DescartarFormulario();
    private async void Guardar(object sender, RoutedEventArgs e)
    {
        if (DataContext is IEditorConClave conClave && EncontrarClave(this)is { } campo)
        {
            using var clave = campo.ObtenerClave();
            await conClave.GuardarConClaveAsync(clave);
            if (DataContext is IEditorCatalogo cuenta)
                Clarisa.Desktop.Presentation.Behaviors.FocoErrorFormulario.Mostrar(this, cuenta.CampoInvalido);
            return;
        }

        if (DataContext is IEditorCatalogo editor)
        {
            await editor.GuardarFormularioAsync();
            Clarisa.Desktop.Presentation.Behaviors.FocoErrorFormulario.Mostrar(this, editor.CampoInvalido);
        }
    }

    private static CampoClaveSeguro? EncontrarClave(DependencyObject padre)
    {
        if (padre is CampoClaveSeguro clave)
            return clave;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(padre); i++)
            if (EncontrarClave(VisualTreeHelper.GetChild(padre, i))is { } encontrada)
                return encontrada;
        return null;
    }

    private void TeclasEditor(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Cerrar(sender, e);
            e.Handled = true;
        }

        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Guardar(sender, e);
            e.Handled = true;
        }
    }
}
