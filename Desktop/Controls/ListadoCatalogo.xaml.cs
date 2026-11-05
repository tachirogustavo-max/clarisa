using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Clarisa.Desktop.Presentation.Models;

namespace Clarisa.Desktop.Controls;
public partial class ListadoCatalogo : UserControl
{
    public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(nameof(Editor), typeof(DataTemplate), typeof(ListadoCatalogo));
    public ListadoCatalogo() => InitializeComponent();
    public DataTemplate Editor { get => (DataTemplate)GetValue(EditorProperty); set => SetValue(EditorProperty, value); }
    public ObservableCollection<DataGridColumn> Columnas => Tabla.Columns;

    private async void Cargar(object sender, RoutedEventArgs e)
    {
        if (DataContext is IListadoCatalogo listado && !listado.EditorAbierto)
            await listado.CargarAsync();
    }

    private async void CambiarAnio(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && DataContext is IListadoCatalogo { Cargando: false } listado)
            await listado.BuscarAsync();
    }

    private async void Buscar(object sender, RoutedEventArgs e)
    {
        if (DataContext is IListadoCatalogo listado)
            await listado.BuscarAsync();
    }

    private void Nuevo(object sender, RoutedEventArgs e) => (DataContext as IEditorCatalogo)?.NuevoRegistro();
    private void EditarSeleccion(object sender, RoutedEventArgs e) => (DataContext as IEditorCatalogo)?.EditarSeleccionado();
    private void Editar(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? origen = e.OriginalSource as DependencyObject;
        while (origen is not null && origen is not DataGridRow)
            origen = VisualTreeHelper.GetParent(origen);
        if (origen is DataGridRow)
            EditarSeleccion(sender, e);
    }

    private void TeclasTabla(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        EditarSeleccion(sender, e);
        e.Handled = true;
    }

    private void TeclasBusqueda(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        Buscar(sender, e);
        e.Handled = true;
    }
}
