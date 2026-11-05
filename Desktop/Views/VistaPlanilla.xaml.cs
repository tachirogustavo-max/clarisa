using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using Clarisa.Desktop.Presentation.Models;

namespace Clarisa.Desktop.Views;
public partial class VistaPlanilla : UserControl
{
    private Action? continuarSalida;
    private ModeloPlanilla? modeloObservado;
    public VistaPlanilla()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => VincularModelo();
        Loaded += (_, _) => VincularModelo();
        Unloaded += (_, _) => Desvincular();
    }
    private void Desvincular()
    {
        if (modeloObservado is not null) modeloObservado.PropertyChanged -= ActualizarEvaluacion;
        modeloObservado = null;
    }
    private void VincularModelo()
    {
        Desvincular();
        modeloObservado = DataContext as ModeloPlanilla;
        if (modeloObservado is not null) modeloObservado.PropertyChanged += ActualizarEvaluacion;
        ActualizarEvaluacion(this, new PropertyChangedEventArgs(null));
    }
    private void ActualizarEvaluacion(object? sender, PropertyChangedEventArgs e)
    {
        ColumnaNumerica.Visibility = modeloObservado?.MostrarNumeros != false ? Visibility.Visible : Visibility.Collapsed;
        ColumnaLetra.Visibility = modeloObservado?.MostrarLetras == true ? Visibility.Visible : Visibility.Collapsed;
    }
    private void DejarPendiente(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ModeloPlanilla { PuedeEditar: true } || sender is not FrameworkElement { DataContext: FilaNota fila }
            || fila.Exonerada || fila.Modalidad == 1) return;
        fila.Texto = "";
        fila.Letra = null;
    }
    public bool SolicitarSalida(Action continuar)
    {
        if (DataContext is ModeloPlanilla { Exoneracion.Visible: true })
        {
            DialogoEx.OrientarSalidaBloqueada();
            return false;
        }
        if (DataContext is not ModeloPlanilla modelo || (modelo.Cambios == 0 && !modelo.Guardando))
            return true;
        modelo.Comparador?.Ocultar();
        continuarSalida = continuar;
        ContenidoPlanilla.IsEnabled = false;
        PanelSalida.Visibility = Visibility.Visible;
        VolverEdicion.Focus();
        return false;
    }
    private void Exonerar(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ModeloPlanilla { Exoneracion: { } exoneracion } modelo) return;
        if (TablaNotas.SelectedItem is FilaNota fila) exoneracion.Abrir(fila);
    }

    private void VolverPlanilla(object sender, RoutedEventArgs e)
    {
        continuarSalida = null;
        PanelSalida.Visibility = Visibility.Collapsed;
        ContenidoPlanilla.IsEnabled = true;
    }

    private void SalirPlanilla(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloPlanilla { Guardando: true })
            return;
        var continuar = continuarSalida;
        continuarSalida = null;
        continuar?.Invoke();
    }

    private void TeclasSalida(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;
        VolverPlanilla(sender, e);
        e.Handled = true;
    }

    private async void Comparar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloPlanilla { Comparador: { } comparador })
            await comparador.ConsultarAsync();
    }

    private async void Guardar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloPlanilla modelo)
            await modelo.GuardarAsync();
    }

    private async void TeclasPlanilla(object sender, KeyEventArgs e)
    {
        if (PanelSalida.Visibility == Visibility.Visible)
            return;
        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control && DataContext is ModeloPlanilla modelo)
        {
            e.Handled = true;
            await modelo.GuardarAsync();
        }
    }

    private void NavegarNota(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Down or Key.Up) || sender is not TextBox { DataContext: FilaNota fila })
            return;
        var indice = TablaNotas.Items.IndexOf(fila);
        var siguiente = indice + (e.Key == Key.Up ? -1 : 1);
        e.Handled = true;
        if (siguiente >= TablaNotas.Items.Count)
        {
            if (BotonGuardar.IsEnabled)
                BotonGuardar.Focus();
            return;
        }

        if (siguiente < 0)
            return;
        var destino = TablaNotas.Items[siguiente];
        TablaNotas.ScrollIntoView(destino);
        TablaNotas.UpdateLayout();
        if (TablaNotas.ItemContainerGenerator.ContainerFromItem(destino)is DataGridRow contenedor)
        {
            var editor = EncontrarEditor(contenedor);
            editor?.Focus();
            editor?.SelectAll();
        }
    }

    private void RevisarErrores(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ModeloPlanilla modelo)
            return;
        var invalida = modelo.Filas.FirstOrDefault(f => f.TieneError);
        if (invalida is null)
            return;
        modelo.MostrarFila(invalida);
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var primera = modelo.Filas.FirstOrDefault(f => f.TieneError);
            if (primera is null)
                return;
            TablaNotas.ScrollIntoView(primera);
            TablaNotas.UpdateLayout();
            if (TablaNotas.ItemContainerGenerator.ContainerFromItem(primera)is DataGridRow contenedor)
            {
                var editor = EncontrarEditor(contenedor);
                editor?.Focus();
                editor?.SelectAll();
            }
        }));
    }

    private static TextBox? EncontrarEditor(DependencyObject origen)
    {
        for (var indice = 0; indice < VisualTreeHelper.GetChildrenCount(origen); indice++)
        {
            var hijo = VisualTreeHelper.GetChild(origen, indice);
            if (hijo is TextBox texto)
                return texto;
            if (EncontrarEditor(hijo)is { } encontrado)
                return encontrado;
        }

        return null;
    }
}
