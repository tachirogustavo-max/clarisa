using System.Windows;
using Clarisa.Instalacion.Contratos;
using Clarisa.Setup.Vistas;

namespace Clarisa.Setup.Servicios;

public sealed class OperadorInterfaz(Window propietario) : IOperadorInstalacion, IOperadorCuentaInicial
{
    private string? usuarioAnterior;
    private string? nombreAnterior;
    public Task<CredencialOperador?> SolicitarClaveOperadorMotorAsync(CancellationToken ct) =>
        MostrarAsync<CredencialOperador?>(() => new DialogoCredencial("liceo_operador", true), ventana => ((DialogoCredencial)ventana).Credencial, ct);

    public Task<CredencialOperador?> SolicitarAccesoPrimeraCuentaAsync(string usuario, CancellationToken ct) =>
        MostrarAsync<CredencialOperador?>(() => new DialogoCredencial(usuario, false), ventana => ((DialogoCredencial)ventana).Credencial, ct);

    public async Task<DatosCuentaInicial?> SolicitarCreacionPrimeraCuentaAsync(string usuario, string? motivoRechazo, CancellationToken ct)
    {
        var cuenta = await MostrarAsync<DatosCuentaInicial?>(() => new DialogoCuentaInicial(usuario, motivoRechazo,
            usuario == usuarioAnterior ? nombreAnterior : null), ventana => ((DialogoCuentaInicial)ventana).Cuenta, ct);
        if (cuenta is not null)
        {
            usuarioAnterior = cuenta.Usuario;
            nombreAnterior = cuenta.Nombre;
        }
        return cuenta;
    }

    public Task<bool> ConfirmarAsync(string mensaje, CancellationToken ct) =>
        MostrarAsync(() => new DialogoConfirmacion(mensaje), _ => true, ct);

    private async Task<T> MostrarAsync<T>(Func<Window> crear, Func<Window, T> leer, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return default!;
        return await propietario.Dispatcher.InvokeAsync(() =>
        {
            if (ct.IsCancellationRequested) return default!;
            var dialogo = crear();
            dialogo.Owner = propietario;
            dialogo.ShowActivated = propietario.ShowActivated;
            using var aviso = ct.Register(() =>
            {
                if (!dialogo.Dispatcher.HasShutdownStarted)
                    dialogo.Dispatcher.BeginInvoke(new Action(() => { if (dialogo.IsVisible) dialogo.Close(); }));
            });
            if (ct.IsCancellationRequested) { dialogo.Close(); return default!; }
            var resultado = dialogo.ShowDialog() == true ? leer(dialogo) : default!;
            if (!ct.IsCancellationRequested) return resultado;
            if (resultado is IDisposable desechable) desechable.Dispose();
            return default!;
        });
    }
}
