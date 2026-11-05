using System.Windows;
using System.Windows.Input;

namespace Clarisa.Desktop.Presentation.Behaviors;

public static class MarcoVentana
{
    public static readonly DependencyProperty ActivoProperty = DependencyProperty.RegisterAttached("Activo", typeof(bool), typeof(MarcoVentana), new PropertyMetadata(false, Cambiar));
    public static void SetActivo(DependencyObject objeto, bool valor) => objeto.SetValue(ActivoProperty, valor);
    public static bool GetActivo(DependencyObject objeto) => (bool)objeto.GetValue(ActivoProperty);

    private static void Cambiar(DependencyObject objeto, DependencyPropertyChangedEventArgs cambio)
    {
        if (objeto is not Window ventana || cambio.NewValue is not true) return;
        ventana.CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => SystemCommands.CloseWindow(ventana)));
        ventana.CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, (_, _) => SystemCommands.MinimizeWindow(ventana),
            (_, e) => e.CanExecute = ventana.ResizeMode != ResizeMode.NoResize));
        ventana.CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, (_, _) =>
        {
            if (ventana.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(ventana);
            else SystemCommands.MaximizeWindow(ventana);
        }, (_, e) => e.CanExecute = ventana.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip));
    }
}
