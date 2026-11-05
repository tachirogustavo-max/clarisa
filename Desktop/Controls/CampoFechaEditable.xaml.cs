using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Clarisa.Desktop.Presentation.Formatting;

namespace Clarisa.Desktop.Controls;

public partial class CampoFechaEditable : UserControl
{
    private bool sincronizando;
    public static readonly DependencyProperty TextoProperty = DependencyProperty.Register(nameof(Texto), typeof(string),
        typeof(CampoFechaEditable), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (objeto, _) => ((CampoFechaEditable)objeto).Sincronizar()));

    public CampoFechaEditable() => InitializeComponent();
    public string Texto { get => (string)GetValue(TextoProperty); set => SetValue(TextoProperty, value); }

    private void Sincronizar()
    {
        if (Calendario is null) return;
        sincronizando = true;
        try
        {
            FechaEscrita.Intentar(Texto, out var fecha);
            Calendario.SelectedDate = fecha;
            if (fecha is { } elegida) Calendario.DisplayDate = elegida;
        }
        finally { sincronizando = false; }
    }

    private void AbrirCalendario(object sender, RoutedEventArgs e)
    {
        Sincronizar();
        if (Calendario.SelectedDate is null) Calendario.DisplayDate = DateTime.Today;
        Desplegable.IsOpen = true;
    }

    private void ElegirFecha(object? sender, SelectionChangedEventArgs e)
    {
        if (sincronizando || Calendario.SelectedDate is not { } fecha) return;
        SetCurrentValue(TextoProperty, FechaEscrita.Formatear(fecha));
        Desplegable.IsOpen = false;
        Entrada.Focus();
    }

    private void TeclasCalendario(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Desplegable.IsOpen = false;
        Entrada.Focus();
        e.Handled = true;
    }

    private void RecibirFoco(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, this)) Entrada.Focus();
    }
}
