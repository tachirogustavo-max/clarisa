using System.Windows;
using System.Windows.Controls;
using Clarisa.Desktop.Presentation.Models;

namespace Clarisa.Desktop.Controls;
public partial class MenuLateral : UserControl
{
    private bool restaurandoSeleccion;
    public event EventHandler<RoutedPropertyChangedEventArgs<ModuloVista>>? NavegacionSolicitada;
    public event EventHandler? CuentaSolicitada;
    public event EventHandler? SalidaSolicitada;
    public static readonly DependencyProperty CompactoProperty = DependencyProperty.Register(nameof(Compacto), typeof(bool), typeof(MenuLateral), new PropertyMetadata(false));
    public bool Compacto { get => (bool)GetValue(CompactoProperty); set => SetValue(CompactoProperty, value); }

    public static readonly DependencyProperty EsAdministracionProperty = DependencyProperty.Register(nameof(EsAdministracion), typeof(bool), typeof(MenuLateral), new PropertyMetadata(false, CambiarPerfil));
    public static readonly DependencyProperty ModuloActualProperty = DependencyProperty.Register(nameof(ModuloActual), typeof(ModuloVista), typeof(MenuLateral), new FrameworkPropertyMetadata(ModuloVista.Inicio, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly DependencyProperty NombreUsuarioProperty = DependencyProperty.Register(nameof(NombreUsuario), typeof(string), typeof(MenuLateral), new PropertyMetadata(""));
    private static readonly DependencyPropertyKey EntradasPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Entradas), typeof(IReadOnlyList<EntradaNavegacion>), typeof(MenuLateral), new PropertyMetadata(null));
    public static readonly DependencyProperty EntradasProperty = EntradasPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey PerfilPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Perfil), typeof(string), typeof(MenuLateral), new PropertyMetadata("Docente"));
    public static readonly DependencyProperty PerfilProperty = PerfilPropertyKey.DependencyProperty;
    public MenuLateral()
    {
        InitializeComponent();
        ActualizarPerfil();
    }

    public bool EsAdministracion { get => (bool)GetValue(EsAdministracionProperty); set => SetValue(EsAdministracionProperty, value); }
    public ModuloVista ModuloActual { get => (ModuloVista)GetValue(ModuloActualProperty); set => SetValue(ModuloActualProperty, value); }
    public string NombreUsuario { get => (string)GetValue(NombreUsuarioProperty); set => SetValue(NombreUsuarioProperty, value); }
    public IReadOnlyList<EntradaNavegacion> Entradas => (IReadOnlyList<EntradaNavegacion>)GetValue(EntradasProperty);
    public string Perfil => (string)GetValue(PerfilProperty);
    private static void CambiarPerfil(DependencyObject objeto, DependencyPropertyChangedEventArgs e) => ((MenuLateral)objeto).ActualizarPerfil();
    private void AbrirCuenta(object sender, RoutedEventArgs e) => OpcionesCuenta.IsOpen = !OpcionesCuenta.IsOpen;
    public void EnfocarIdentidad() => BotonIdentidad.Focus();
    private void EnfocarCuenta(object? sender, EventArgs e) => BotonClave.Focus();
    private void CambiarClave(object sender, RoutedEventArgs e) { OpcionesCuenta.IsOpen = false; CuentaSolicitada?.Invoke(this, EventArgs.Empty); }
    private void SalirCuenta(object sender, RoutedEventArgs e) { OpcionesCuenta.IsOpen = false; SalidaSolicitada?.Invoke(this, EventArgs.Empty); }
    private void TeclaCuenta(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape) { OpcionesCuenta.IsOpen = false; BotonIdentidad.Focus(); e.Handled = true; }
    }
    private void ActualizarPerfil()
    {
        SetValue(EntradasPropertyKey, NavegacionPerfil.Crear(EsAdministracion));
        SetValue(PerfilPropertyKey, EsAdministracion ? "Administrador" : "Docente");
    }

    private void Navegar(object sender, SelectionChangedEventArgs e)
    {
        // La selección también se sincroniza al cambiar ModuloActual; eso no es una petición del usuario.
        if (restaurandoSeleccion || e.AddedItems.Count == 0 || e.AddedItems[0] is not EntradaNavegacion entrada || entrada.Clave == ModuloActual)
            return;
        restaurandoSeleccion = true;
        ((ListBox)sender).SetCurrentValue(ListBox.SelectedValueProperty, ModuloActual);
        restaurandoSeleccion = false;
        NavegacionSolicitada?.Invoke(this, new RoutedPropertyChangedEventArgs<ModuloVista>(ModuloActual, entrada.Clave));
    }

    // Pulsar el módulo ya seleccionado no produce SelectionChanged; el escritorio decide si hay un detalle del que volver.
    private void PulsarEntrada(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { IsSelected: true, DataContext: EntradaNavegacion entrada } && entrada.Clave == ModuloActual)
            SolicitarActual();
    }

    private void TeclaEntrada(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is not (System.Windows.Input.Key.Enter or System.Windows.Input.Key.Space))
            return;
        if (sender is ListBoxItem { IsSelected: true, DataContext: EntradaNavegacion entrada } && entrada.Clave == ModuloActual)
        {
            SolicitarActual();
            e.Handled = true;
        }
    }

    private void SolicitarActual() => NavegacionSolicitada?.Invoke(this, new RoutedPropertyChangedEventArgs<ModuloVista>(ModuloActual, ModuloActual));
}
