using System.Runtime.InteropServices;
using System.Security;
using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Presentation.Models;
public sealed class ModeloClavePropia(IClienteClavePropia? cliente = null) : ModeloObservable
{
    public ModeloClavePropia() : this(null) { }
    private readonly CancellationTokenSource cierre = new();
    public bool Guardando { get; private set; }
    public bool Disponible => !Guardando;
    public string Mensaje { get; private set; } = "";
    public string ErrorActual { get; private set; } = "";
    public string ErrorNueva { get; private set; } = "";
    public string ErrorConfirmacion { get; private set; } = "";
    public void Limpiar()
    {
        if (Guardando) return;
        Mensaje = ErrorActual = ErrorNueva = ErrorConfirmacion = "";
        Actualizar();
    }
    public async Task<ResultadoClavePropia?> CambiarAsync(SecureString actual, SecureString nueva, SecureString confirmacion)
    {
        if (Guardando || cierre.IsCancellationRequested) return null;
        Limpiar();
        ErrorActual = actual.Length == 0 ? "Escribe tu contraseña actual." : "";
        ErrorNueva = nueva.Length == 0 ? "Escribe una contraseña nueva." : "";
        ErrorConfirmacion = Coinciden(nueva, confirmacion) ? "" : "Las contraseñas nuevas no coinciden.";
        Actualizar();
        if (ErrorActual.Length + ErrorNueva.Length + ErrorConfirmacion.Length != 0) return null;
        if (cliente is null) { Mensaje = "Conecta con el servidor para cambiar tu contraseña."; Actualizar(); return null; }
        Guardando = true;
        Mensaje = "Actualizando contraseña…";
        Actualizar();
        try
        {
            var resultado = await cliente.CambiarAsync(actual, nueva, cierre.Token);
            cierre.Token.ThrowIfCancellationRequested();
            Mensaje = resultado.Mensaje;
            if (resultado.Campo == "ClaveActual") ErrorActual = resultado.Mensaje;
            if (resultado.Campo == "ClaveNueva") ErrorNueva = resultado.Mensaje;
            return resultado;
        }
        catch (OperationCanceledException) when (cierre.IsCancellationRequested) { return null; }
        finally { Guardando = false; Actualizar(); }
    }
    public void Cancelar() => cierre.Cancel();
    private void Actualizar()
    {
        foreach (var nombre in new[] { nameof(Guardando), nameof(Disponible), nameof(Mensaje), nameof(ErrorActual), nameof(ErrorNueva), nameof(ErrorConfirmacion) }) Notificar(nombre);
    }
    private static bool Coinciden(SecureString primera, SecureString segunda)
    {
        if (primera.Length != segunda.Length) return false;
        var a = IntPtr.Zero;
        var b = IntPtr.Zero;
        try
        {
            a = Marshal.SecureStringToGlobalAllocUnicode(primera);
            b = Marshal.SecureStringToGlobalAllocUnicode(segunda);
            var diferencia = 0;
            for (var i = 0; i < primera.Length; i++) diferencia |= Marshal.ReadInt16(a, i * 2) ^ Marshal.ReadInt16(b, i * 2);
            return diferencia == 0;
        }
        finally
        {
            if (a != IntPtr.Zero) Marshal.ZeroFreeGlobalAllocUnicode(a);
            if (b != IntPtr.Zero) Marshal.ZeroFreeGlobalAllocUnicode(b);
        }
    }
}
