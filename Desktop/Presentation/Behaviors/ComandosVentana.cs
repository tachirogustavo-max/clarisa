using System.Windows.Input;

namespace Clarisa.Desktop.Presentation.Behaviors;

public static class ComandosVentana
{
    public static RoutedUICommand MostrarGuia { get; } = new("Guía rápida", nameof(MostrarGuia), typeof(ComandosVentana),
        new InputGestureCollection { new KeyGesture(Key.F1) });
    public static RoutedUICommand VolverMaterias { get; } = new("Volver a materias", nameof(VolverMaterias), typeof(ComandosVentana),
        new InputGestureCollection { new KeyGesture(Key.Left, ModifierKeys.Alt) });
}
