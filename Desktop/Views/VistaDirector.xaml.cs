using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using Clarisa.Desktop.Presentation.Models;

namespace Clarisa.Desktop.Views;
public partial class VistaDirector : UserControl
{
    public static readonly DependencyProperty MostrarCabeceraProperty = DependencyProperty.Register(nameof(MostrarCabecera), typeof(bool), typeof(VistaDirector), new PropertyMetadata(true));
    public bool MostrarCabecera { get => (bool)GetValue(MostrarCabeceraProperty); set => SetValue(MostrarCabeceraProperty, value); }

    private ModeloDirector? observado;
    public VistaDirector()
    {
        InitializeComponent();
        Loaded += AlCargar;
        Unloaded += AlSalir;
        DataContextChanged += (_, _) =>
        {
            if (IsLoaded)
                ObservarModelo(DataContext as ModeloDirector);
        };
    }

    private void AlCargar(object sender, RoutedEventArgs e)
    {
        ObservarModelo(DataContext as ModeloDirector);
    }

    private void AlSalir(object sender, RoutedEventArgs e)
    {
        ObservarModelo(null);
    }

    private void ObservarModelo(ModeloDirector? modelo)
    {
        if (ReferenceEquals(observado, modelo))
            return;
        if (observado is not null)
            observado.PropertyChanged -= CambioModelo;
        observado = modelo;
        if (observado is not null)
        {
            observado.PropertyChanged += CambioModelo;
            if (observado.ConfirmacionVisible)
                VolverConfirmacion.Focus();
        }
    }

    private void CambioModelo(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ModeloDirector.ConfirmacionVisible) && DataContext is ModeloDirector { ConfirmacionVisible: true })
            Dispatcher.BeginInvoke(new Action(() => VolverConfirmacion.Focus()));
    }

    private void TeclasConfirmacion(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is ModeloDirector modelo)
        {
            modelo.CancelarCambio.Execute(null);
            e.Handled = true;
        }
    }

    private async void Confirmar(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloDirector modelo)
            await modelo.ConfirmarAsync();
    }

    private async void ComprobarEstado(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModeloDirector modelo && sender is FrameworkElement { DataContext: LapsoVisual lapso })
            await modelo.ComprobarEstadoAsync(lapso);
    }
}
