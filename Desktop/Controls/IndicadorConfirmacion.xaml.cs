using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Clarisa.Desktop.Controls;
public partial class IndicadorConfirmacion : UserControl
{
    public static readonly DependencyProperty ActivoProperty = DependencyProperty.Register(nameof(Activo), typeof(bool), typeof(IndicadorConfirmacion), new PropertyMetadata(false, CambiarEstado));
    public IndicadorConfirmacion() => InitializeComponent();
    public bool Activo { get => (bool)GetValue(ActivoProperty); set => SetValue(ActivoProperty, value); }

    private static void CambiarEstado(DependencyObject objeto, DependencyPropertyChangedEventArgs cambio)
    {
        if (objeto is not IndicadorConfirmacion indicador || cambio.NewValue is not true || !indicador.IsInitialized || DesignerProperties.GetIsInDesignMode(indicador) || !SystemParameters.ClientAreaAnimation)
            return;
        indicador.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { FillBehavior = FillBehavior.Stop });
    }
}
