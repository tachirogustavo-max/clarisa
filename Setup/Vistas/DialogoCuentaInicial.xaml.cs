using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using Clarisa.Instalacion.Contratos;

namespace Clarisa.Setup.Vistas;

public partial class DialogoCuentaInicial : Window
{
    private readonly string usuario = "";
    public DatosCuentaInicial? Cuenta { get; private set; }

    public DialogoCuentaInicial()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (!ShowActivated) return;
            if (Nombre.Text.Length > 0 && Error.Text.Length > 0) Clave.Focus();
            else Nombre.Focus();
        };
        Closed += (_, _) =>
        {
            Clave.Clear();
            Confirmacion.Clear();
            if (DialogResult == true) return;
            Cuenta?.Dispose();
            Cuenta = null;
        };
    }

    public DialogoCuentaInicial(string usuario, string? motivoRechazo = null, string? nombreAnterior = null) : this()
    {
        this.usuario = usuario;
        Usuario.Text = usuario;
        Error.Text = motivoRechazo ?? "";
        Nombre.MaxLength = DatosCuentaInicial.MaximoNombre;
        Nombre.Text = nombreAnterior ?? "";
    }

    private void Aceptar(object sender, RoutedEventArgs e)
    {
        if (Cuenta is not null) return;
        var nombre = Nombre.Text.Trim();
        if (nombre.Length is 0 or > DatosCuentaInicial.MaximoNombre || nombre.Any(char.IsControl))
        {
            Error.Text = "Escribe un nombre de entre 1 y 80 caracteres, sin saltos de línea.";
            Nombre.Focus();
            return;
        }
        using var clave = Clave.SecurePassword;
        using var repetida = Confirmacion.SecurePassword;
        if (clave.Length is < 15 or > 256)
        {
            Error.Text = "Usa una contraseña de entre 15 y 256 caracteres.";
            Clave.Focus();
            return;
        }
        if (clave.Length != repetida.Length)
        {
            Error.Text = "Las contraseñas no coinciden. Escríbelas de nuevo.";
            Confirmacion.Focus();
            return;
        }
        var memoria = Marshal.SecureStringToGlobalAllocUnicode(clave);
        var otra = IntPtr.Zero;
        var copia = new char[clave.Length];
        var confirmacion = new char[repetida.Length];
        try
        {
            otra = Marshal.SecureStringToGlobalAllocUnicode(repetida);
            Marshal.Copy(memoria, copia, 0, copia.Length);
            Marshal.Copy(otra, confirmacion, 0, confirmacion.Length);
            if (!CryptographicOperations.FixedTimeEquals(MemoryMarshal.AsBytes(copia.AsSpan()), MemoryMarshal.AsBytes(confirmacion.AsSpan())))
            {
                Error.Text = "Las contraseñas no coinciden. Escríbelas de nuevo.";
                Confirmacion.Focus();
                return;
            }
            if (copia.AsSpan().Equals(usuario.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                Error.Text = "Elige una contraseña distinta del usuario de acceso.";
                Clave.Focus();
                return;
            }
            Cuenta = new DatosCuentaInicial(usuario, nombre, copia);
            DialogResult = true;
        }
        catch (ArgumentException)
        {
            Error.Text = "Revisa el nombre y la contraseña antes de continuar.";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(copia.AsSpan()));
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(confirmacion.AsSpan()));
            Marshal.ZeroFreeGlobalAllocUnicode(memoria);
            if (otra != IntPtr.Zero) Marshal.ZeroFreeGlobalAllocUnicode(otra);
        }
    }
}
