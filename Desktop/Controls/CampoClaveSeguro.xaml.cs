using System.Security;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace Clarisa.Desktop.Controls;
public partial class CampoClaveSeguro : UserControl
{
    public event EventHandler? ClaveCambiada;
    public bool TieneContenido
    {
        get
        {
            if (Alternar.IsChecked == true)
                return Visible.Text.Length > 0;
            using var copia = Oculta.SecurePassword;
            return copia.Length > 0;
        }
    }

    // La plantilla del editor reutiliza el control entre registros; un formulario distinto nunca hereda la clave anterior.
    public static readonly DependencyProperty FormularioAsociadoProperty = DependencyProperty.Register(nameof(FormularioAsociado), typeof(object), typeof(CampoClaveSeguro), new PropertyMetadata(null, (campo, _) => ((CampoClaveSeguro)campo).Limpiar()));
    public static readonly DependencyProperty MayusculasActivasProperty = DependencyProperty.Register(nameof(MayusculasActivas), typeof(bool), typeof(CampoClaveSeguro), new PropertyMetadata(false));
    public CampoClaveSeguro()
    {
        InitializeComponent();
        Visible.TextChanged += (_, _) => ClaveCambiada?.Invoke(this, EventArgs.Empty);
        Oculta.PasswordChanged += (_, _) => ClaveCambiada?.Invoke(this, EventArgs.Empty);
        Unloaded += (_, _) => Limpiar();
    }

    public object? FormularioAsociado { get => GetValue(FormularioAsociadoProperty); set => SetValue(FormularioAsociadoProperty, value); }
    public bool MayusculasActivas { get => (bool)GetValue(MayusculasActivasProperty); private set => SetValue(MayusculasActivasProperty, value); }

    public SecureString ObtenerClave()
    {
        if (Alternar.IsChecked != true)
        {
            var oculta = Oculta.SecurePassword;
            oculta.MakeReadOnly();
            return oculta;
        }

        var copia = new SecureString();
        foreach (var letra in Visible.Text)
            copia.AppendChar(letra);
        copia.MakeReadOnly();
        return copia;
    }

    public void Limpiar()
    {
        Visible.Clear();
        Oculta.Clear();
        Alternar.IsChecked = false;
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        if (ReferenceEquals(e.NewFocus, this))
        {
            if (Alternar.IsChecked == true)
                Visible.Focus();
            else
                Oculta.Focus();
        }
    }

    private void MostrarClave(object sender, RoutedEventArgs e)
    {
        Visible.Text = Oculta.Password;
        Oculta.Clear();
        Oculta.Visibility = Visibility.Collapsed;
        Visible.Visibility = Visibility.Visible;
        AutomationProperties.SetName(Alternar, "Ocultar contraseña");
        Alternar.ToolTip = "Ocultar contraseña";
    }

    private void OcultarClave(object sender, RoutedEventArgs e)
    {
        Oculta.Password = Visible.Text;
        Visible.Clear();
        Visible.Visibility = Visibility.Collapsed;
        Oculta.Visibility = Visibility.Visible;
        AutomationProperties.SetName(Alternar, "Mostrar contraseña");
        Alternar.ToolTip = "Mostrar contraseña";
    }

    private void ActualizarMayusculas(object sender, RoutedEventArgs e) => MayusculasActivas = Keyboard.IsKeyToggled(Key.CapsLock);
}
