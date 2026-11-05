using System.Windows;
using System.Windows.Controls;
using Clarisa.Desktop.Presentation.Models;

namespace Clarisa.Desktop.Controls;
public partial class DialogoComparacionNotas : UserControl
{
    public DialogoComparacionNotas() => InitializeComponent();
    private void Aplicar(object sender, RoutedEventArgs e) => (DataContext as ComparadorNotas)?.Aplicar();
    private void Cancelar(object sender, RoutedEventArgs e) => (DataContext as ComparadorNotas)?.Ocultar();
}
