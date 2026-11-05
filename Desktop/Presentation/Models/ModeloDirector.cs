using System.Collections.ObjectModel;
using System.ComponentModel;
using Clarisa.Desktop.Presentation.Commands;
using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Presentation.Models;
public class ModeloDirector : INotifyPropertyChanged, ISalidaProtegida
{
    private readonly IClienteVentanas? cliente;
    private readonly CancellationTokenSource cierre = new();
    private LapsoVisual? solicitud;
    public event PropertyChangedEventHandler? PropertyChanged;
    public ModeloDirector() : this(null, [])
    {
    }

    public ModeloDirector(IClienteVentanas? cliente, IEnumerable<LapsoVisual> lapsos)
    {
        this.cliente = cliente;
        foreach (var lapso in lapsos)
            Lapsos.Add(lapso);
        SolicitarCambio = new Comando(p => PedirCambio((LapsoVisual)p!), p => p is LapsoVisual lapso && Lapsos.Contains(lapso) && !lapso.SinConfirmar && !Ocupado && solicitud is null && cliente is not null);
        CancelarCambio = new Comando(_ =>
        {
            solicitud = null;
            Actualizar();
        }, _ => !Ocupado);
    }

    public ObservableCollection<LapsoVisual> Lapsos { get; } = [];
    public Comando SolicitarCambio { get; }
    public Comando CancelarCambio { get; }
    public string AnioEscolar { get; set; } = "Año escolar";
    public bool Ocupado { get; private set; }
    public bool ConfirmacionVisible => solicitud is not null;
    public bool PuedeConfirmar => solicitud is not null && !Ocupado;
    public string Mensaje { get; private set; } = "";
    public string TituloConfirmacion => solicitud is null ? "" : solicitud.Accion + " · " + solicitud.Nombre;
    public string TextoConfirmacion => solicitud?.CargaAbierta == true ? "Los docentes ya no podrán guardar nuevas notas en este momento. Las notas registradas se conservan." : "Los docentes asignados podrán cargar y modificar las notas de este momento hasta que cierres la carga.";
    public string TextoAccion => Ocupado ? "Confirmando…" : solicitud?.Accion ?? "Confirmar";

    private void PedirCambio(LapsoVisual lapso)
    {
        solicitud = lapso;
        Mensaje = "";
        Actualizar();
    }

    public async Task ConfirmarAsync()
    {
        if (!PuedeConfirmar || cliente is null || cierre.IsCancellationRequested)
            return;
        var lapso = solicitud!;
        var abrir = !lapso.CargaAbierta;
        Ocupado = true;
        Actualizar();
        try
        {
            var respuesta = await cliente.CambiarAsync(lapso.Id, abrir, lapso.Version, cierre.Token);
            if (respuesta.Datos is not { } datos || datos.LapsoId != lapso.Id || datos.PermiteCarga != abrir || datos.Version <= lapso.Version)
            {
                lapso.MarcarSinConfirmar();
                Mensaje = respuesta.Error ?? "No llegó una confirmación completa. El estado queda pendiente de comprobar.";
            }
            else
            {
                lapso.Actualizar(datos.PermiteCarga, datos.Version);
                Mensaje = lapso.Nombre + ": " + lapso.Estado.ToLowerInvariant() + ".";
            }

            solicitud = null;
        }
        catch (OperationCanceledException)when (cierre.IsCancellationRequested)
        {
        }
        finally
        {
            Ocupado = false;
            Actualizar();
        }
    }

    public void Cancelar() => cierre.Cancel();
    public bool SolicitarSalida(Action continuar)
    {
        if (Ocupado)
        {
            Mensaje = "Espera la respuesta del servidor antes de salir.";
            Actualizar();
            return false;
        }

        if (ConfirmacionVisible)
            CancelarCambio.Execute(null);
        return true;
    }

    public async Task ComprobarEstadoAsync(LapsoVisual lapso)
    {
        if (cliente is null || Ocupado || solicitud is not null || !Lapsos.Contains(lapso) || cierre.IsCancellationRequested)
            return;
        Ocupado = true;
        Actualizar();
        try
        {
            var respuesta = await cliente.ConsultarAsync(lapso.Id, cierre.Token);
            if (respuesta.Datos is { } datos && datos.LapsoId == lapso.Id && datos.Version >= 0 && (!datos.PermiteCarga || datos.Version > 0))
            {
                lapso.Actualizar(datos.PermiteCarga, datos.Version);
                Mensaje = "Estado actualizado desde el servidor.";
            }
            else
                Mensaje = respuesta.Error ?? "No se pudo comprobar el estado. No se repitió el cambio.";
        }
        catch (OperationCanceledException)when (cierre.IsCancellationRequested)
        {
        }
        finally
        {
            Ocupado = false;
            Actualizar();
        }
    }

    private void Actualizar()
    {
        foreach (var nombre in new[]
        {
            nameof(Ocupado),
            nameof(ConfirmacionVisible),
            nameof(PuedeConfirmar),
            nameof(Mensaje),
            nameof(TituloConfirmacion),
            nameof(TextoConfirmacion),
            nameof(TextoAccion)
        }

        )
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
        SolicitarCambio.Actualizar();
        CancelarCambio.Actualizar();
    }
}
