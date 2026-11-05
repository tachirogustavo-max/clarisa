using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using Clarisa.Instalacion.Contratos;
namespace Clarisa.Setup.Vistas;
public partial class DialogoCredencial : Window
{
    private readonly string usuario;
    private readonly bool confirmar;
    public CredencialOperador? Credencial { get; private set; }
    public DialogoCredencial(string usuario, bool confirmar)
    {
        InitializeComponent();
        this.usuario = usuario;
        this.confirmar = confirmar;
        Encabezado.Text = confirmar ? "Protege el mantenimiento" : "Comprueba tu acceso";
        Descripcion.Text = confirmar ? "Elige una contraseña para el mantenimiento del servidor. Consérvala en un lugar seguro; no es la contraseña de la aplicación."
            : "Escribe la contraseña de la cuenta «" + usuario + "» para comprobar que ya puede entrar al sistema.";
        PanelConfirmacion.Visibility = confirmar ? Visibility.Visible : Visibility.Collapsed;
        Loaded += (_, _) => { if (ShowActivated) Clave.Focus(); };
        Closed += (_, _) => { Clave.Clear(); Confirmacion.Clear(); };
    }
    private void Aceptar(object sender, RoutedEventArgs e)
    {
        using var clave = Clave.SecurePassword;
        using var repetida = Confirmacion.SecurePassword;
        if (clave.Length == 0 || confirmar && clave.Length < 15) { Error.Text = confirmar ? "Usa al menos 15 caracteres." : "Escribe la contraseña."; return; }
        var memoria = Marshal.SecureStringToGlobalAllocUnicode(clave);
        var copia = new char[clave.Length];
        var otra = IntPtr.Zero;
        try
        {
            Marshal.Copy(memoria, copia, 0, copia.Length);
            if (confirmar)
            {
                if (clave.Length != repetida.Length) { Error.Text = "Las contraseñas no coinciden."; return; }
                otra = Marshal.SecureStringToGlobalAllocUnicode(repetida);
                for (var i = 0; i < copia.Length; i++)
                    if (copia[i] != (char)Marshal.ReadInt16(otra, i * 2)) { Error.Text = "Las contraseñas no coinciden."; return; }
            }
            Credencial = new CredencialOperador(usuario, copia);
            DialogResult = true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(copia.AsSpan()));
            Marshal.ZeroFreeGlobalAllocUnicode(memoria);
            if (otra != IntPtr.Zero) Marshal.ZeroFreeGlobalAllocUnicode(otra);
        }
    }
}
