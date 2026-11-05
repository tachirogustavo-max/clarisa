using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;

namespace Clarisa.Desktop.Presentation.Behaviors;
public static class AnimacionEntrada
{
    public static readonly DependencyProperty ActivaProperty = DependencyProperty.RegisterAttached("Activa", typeof(bool), typeof(AnimacionEntrada), new PropertyMetadata(false, Cambiar));
    public static bool GetActiva(DependencyObject elemento) => (bool)elemento.GetValue(ActivaProperty);
    public static void SetActiva(DependencyObject elemento, bool valor) => elemento.SetValue(ActivaProperty, valor);
    private static void Cambiar(DependencyObject objeto, DependencyPropertyChangedEventArgs cambio)
    {
        if (objeto is not FrameworkElement elemento)
            return;
        elemento.Loaded -= AlMostrar;
        if (cambio.NewValue is true)
            elemento.Loaded += AlMostrar;
    }

    private static void AlMostrar(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement elemento || DesignerProperties.GetIsInDesignMode(elemento) || !SystemParameters.ClientAreaAnimation)
            return;
        elemento.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { FillBehavior = FillBehavior.Stop });
    }
}
