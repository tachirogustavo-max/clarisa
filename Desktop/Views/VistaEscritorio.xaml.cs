using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clarisa.Desktop.Presentation.Models;

namespace Clarisa.Desktop.Views;
public partial class VistaEscritorio : UserControl
{
    public event EventHandler? CerrarSesion;
    public bool PuedeMostrarGuia => DataContext is ModeloEscritorio { PuedeCerrarSesion: true }
        && PanelCuenta.Visibility != Visibility.Visible;
    public bool PuedeVolverMaterias => PuedeMostrarGuia && PanelGuia.Visibility != Visibility.Visible
        && DataContext is ModeloEscritorio { MostrandoPlanilla: true };
    public VistaEscritorio()
    {
        InitializeComponent();
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
    }

    public bool SolicitarSalida(Action continuar)
    {
        if (BuscarPlanilla(ContenidoModulo)is { } planilla)
            return planilla.SolicitarSalida(continuar);
        if (DataContext is ModeloEscritorio { Contenido: ISalidaProtegida protector })
            return protector.SolicitarSalida(continuar);
        return true;
    }

    public void MostrarGuia()
    {
        if (!PuedeMostrarGuia || DataContext is not ModeloEscritorio modelo) return;
        PanelGuia.DataContext = modelo;
        ContenidoModulo.IsHitTestVisible = false;
        PanelGuia.Visibility = Visibility.Visible;
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        PanelGuia.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.First));
    }
    private void CerrarGuia(object? sender, EventArgs e)
    {
        var estabaAbierta = PanelGuia.Visibility == Visibility.Visible;
        PanelGuia.Visibility = Visibility.Collapsed;
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        ContenidoModulo.IsHitTestVisible = true;
        if (estabaAbierta && Window.GetWindow(this) is { } ventana
            && ventana.Template.FindName("AyudaVentana", ventana) is Button boton) boton.Focus();
    }
    private void AbrirCuenta(object? sender, EventArgs e)
    {
        if (DataContext is not ModeloEscritorio modelo || modelo.CerrandoSesion) return;
        if (PanelCuenta.Visibility == Visibility.Visible) return;
        void Mostrar()
        {
            CerrarGuia(this, EventArgs.Empty);
            modelo.CambioClave.Limpiar();
            FormularioClave.LimpiarCampos();
            PanelCuenta.Visibility = Visibility.Visible;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            ContenidoModulo.IsHitTestVisible = false;
            FormularioClave.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.First));
        }
        if (SolicitarSalida(Mostrar)) Mostrar();
    }
    private void CerrarCuenta(object? sender, EventArgs e)
    {
        FormularioClave.LimpiarCampos();
        PanelCuenta.Visibility = Visibility.Collapsed;
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        ContenidoModulo.IsHitTestVisible = true;
        MenuPrincipal.EnfocarIdentidad();
    }
    private void SalirTrasClave(object? sender, string aviso)
    {
        if (DataContext is ModeloEscritorio modelo) modelo.AvisoSalida = aviso;
        CerrarSesion?.Invoke(this, EventArgs.Empty);
    }

    private void Navegar(object? sender, RoutedPropertyChangedEventArgs<ModuloVista> e)
    {
        if (PanelCuenta.Visibility == Visibility.Visible) return;
        CerrarGuia(this, EventArgs.Empty);
        if (DataContext is not ModeloEscritorio modelo)
            return;
        if (e.NewValue == modelo.ModuloActual && !modelo.MostrandoDetalle)
            return;
        if (SolicitarSalida(() => modelo.Abrir(e.NewValue)))
            modelo.Abrir(e.NewValue);
    }

    private void Salir(object? sender, EventArgs e)
    {
        if (SolicitarSalida(() => CerrarSesion?.Invoke(this, EventArgs.Empty)))
            CerrarSesion?.Invoke(this, EventArgs.Empty);
    }

    public void VolverMaterias()
    {
        if (!PuedeVolverMaterias || DataContext is not ModeloEscritorio modelo)
            return;
        if (SolicitarSalida(() => modelo.Abrir(ModuloVista.Calificaciones)))
            modelo.Abrir(ModuloVista.Calificaciones);
    }

    private static VistaPlanilla? BuscarPlanilla(DependencyObject padre)
    {
        if (padre is VistaPlanilla vista)
            return vista;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(padre); i++)
            if (BuscarPlanilla(VisualTreeHelper.GetChild(padre, i))is { } encontrada)
                return encontrada;
        return null;
    }
}
