using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Clarisa.Desktop.Presentation.Behaviors;
public static class FocoModal
{
    public static readonly DependencyProperty ActivoProperty = DependencyProperty.RegisterAttached("Activo", typeof(bool), typeof(FocoModal), new PropertyMetadata(false, Cambiar));
    public static bool GetActivo(DependencyObject elemento) => (bool)elemento.GetValue(ActivoProperty);
    public static void SetActivo(DependencyObject elemento, bool valor) => elemento.SetValue(ActivoProperty, valor);
    private static void Cambiar(DependencyObject objeto, DependencyPropertyChangedEventArgs cambio)
    {
        if (objeto is not FrameworkElement elemento || cambio.NewValue is not true)
            return;
        KeyboardNavigation.SetTabNavigation(elemento, KeyboardNavigationMode.Cycle);
        elemento.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (elemento.IsVisible && Window.GetWindow(elemento)?.IsActive == true)
                elemento.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }));
    }
}
