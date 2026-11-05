using System.Windows.Controls;
using Clarisa.Desktop.Presentation.Models;

namespace Clarisa.Desktop.Views;
public partial class VistaArea : UserControl
{
    private bool restaurando;
    public VistaArea() => InitializeComponent();
    private void Cambiar(object sender, SelectionChangedEventArgs e)
    {
        if (restaurando || !ReferenceEquals(e.OriginalSource, sender) || DataContext is not ModeloArea area || e.AddedItems.Count == 0 || e.AddedItems[0] is not EntradaArea entrada || entrada == area.Seleccionada)
            return;
        restaurando = true;
        ((TabControl)sender).SetCurrentValue(TabControl.SelectedItemProperty, area.Seleccionada);
        restaurando = false;
        area.Abrir(entrada);
        e.Handled = true;
    }
}
