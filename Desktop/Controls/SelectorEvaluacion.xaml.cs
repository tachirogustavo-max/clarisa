using System.Windows;
using System.Windows.Controls;
namespace Clarisa.Desktop.Controls;
public partial class SelectorEvaluacion : UserControl
{
    public static readonly DependencyProperty ModalidadProperty = DependencyProperty.Register(nameof(Modalidad), typeof(int), typeof(SelectorEvaluacion),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public int Modalidad { get => (int)GetValue(ModalidadProperty); set => SetValue(ModalidadProperty, value); }
    public SelectorEvaluacion() => InitializeComponent();
}
