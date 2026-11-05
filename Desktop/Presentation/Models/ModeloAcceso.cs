using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security;
using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Presentation.Models;
public sealed class ModeloAcceso : INotifyPropertyChanged
{
    private readonly IClienteSesiones? cliente;
    private readonly CancellationTokenSource cierre = new();
    private string usuario = "";
    private string servidor = DireccionServidor.Predeterminada;
    public event PropertyChangedEventHandler? PropertyChanged;
    public ModeloAcceso()
    {
    }

    public ModeloAcceso(IClienteSesiones cliente, string? aviso = null)
    {
        this.cliente = cliente;
        ErrorGeneral = aviso ?? "";
    }
    public string Usuario
    {
        get => usuario;
        set
        {
            usuario = value;
            Notificar();
        }
    }

    public string Servidor
    {
        get => servidor;
        set
        {
            servidor = value;
            Notificar();
        }
    }

    public string ErrorUsuario { get; private set; } = "";
    public string ErrorClave { get; private set; } = "";
    public string ErrorServidor { get; private set; } = "";
    public string ErrorGeneral { get; private set; } = "";
    public string Estado { get; private set; } = "";
    public bool Disponible { get; private set; } = true;
    public string TextoBoton => Disponible ? "Entrar" : "Conectando…";
    public SesionActual? Sesion { get; private set; }
    public Uri? ServidorSesion { get; private set; }
    public bool UltimoIntentoEnviado { get; private set; }

    public async Task IniciarSesionAsync(SecureString clave)
    {
        if (!Disponible || cierre.IsCancellationRequested)
            return;
        UltimoIntentoEnviado = false;
        ErrorUsuario = string.IsNullOrWhiteSpace(Usuario) ? "Escribe tu usuario." : "";
        ErrorClave = clave.Length == 0 ? "Escribe tu contraseña." : "";
        var destino = DireccionServidor.Validar(Servidor);
        ErrorServidor = destino.Error ?? "";
        ErrorGeneral = "";
        Estado = "";
        Sesion = null;
        ServidorSesion = null;
        NotificarFormulario();
        if (ErrorUsuario.Length + ErrorClave.Length + ErrorServidor.Length > 0)
            return;
        if (cliente is null)
            return;
        Disponible = false;
        UltimoIntentoEnviado = true;
        NotificarFormulario();
        try
        {
            var resultado = await cliente.IniciarAsync(destino.Direccion!, Usuario.Trim(), clave, cierre.Token);
            if (cierre.IsCancellationRequested)
                return;
            if (DireccionServidor.Validar(Servidor).Direccion != destino.Direccion)
            {
                ErrorGeneral = "La dirección cambió durante la conexión. Vuelve a entrar con el servidor seleccionado.";
                return;
            }
            Sesion = resultado.Sesion;
            ServidorSesion = Sesion is null ? null : destino.Direccion;
            ErrorGeneral = resultado.Error ?? "";
            Estado = Sesion is null ? "" : "Sesión iniciada.";
        }
        catch (OperationCanceledException)when (cierre.IsCancellationRequested)
        {
        }
        finally
        {
            Disponible = true;
            NotificarFormulario();
        }
    }

    public void Cancelar() => cierre.Cancel();
    private void NotificarFormulario()
    {
        foreach (var propiedad in new[]
        {
            nameof(ErrorUsuario),
            nameof(ErrorClave),
            nameof(ErrorServidor),
            nameof(ErrorGeneral),
            nameof(Estado),
            nameof(Disponible),
            nameof(TextoBoton),
            nameof(Sesion)
        }

        )
            Notificar(propiedad);
    }

    private void Notificar([CallerMemberName] string? propiedad = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
}
