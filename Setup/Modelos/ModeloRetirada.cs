using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using Clarisa.Instalacion.Contratos;

namespace Clarisa.Setup.Modelos;

public sealed class ModeloRetirada : ModeloObservable, IModeloProgreso
{
    private readonly CancellationTokenSource cierre = new();
    private readonly Func<SolicitudRetirada, CancellationToken, Task<RevisionRetirada>>? revisar;
    private readonly Func<RevisionRetirada, IProgress<AvanceInstalacion>, CancellationToken, Task<ResultadoRetirada>>? retirar;
    private RevisionRetirada? revision;
    private bool ocupado;
    private bool ejecutando;
    private bool iniciado;
    private bool progresoVisible;
    private bool finalizado;
    private bool mostrarDetalles;
    private PasoInstalacionVisible? pasoActual;
    private string mensaje = "Comprobando la instalación antes de proponer cambios.";

    public ModeloRetirada() { }
    public ModeloRetirada(string problema) { mensaje = problema; iniciado = true; }
    public ModeloRetirada(
        Func<SolicitudRetirada, CancellationToken, Task<RevisionRetirada>> revisar,
        Func<RevisionRetirada, IProgress<AvanceInstalacion>, CancellationToken, Task<ResultadoRetirada>> retirar)
    {
        this.revisar = revisar;
        this.retirar = retirar;
    }

    public ObservableCollection<PasoInstalacionVisible> Pasos { get; } = [];
    public IReadOnlyList<ElementoRetirada> SeRetira => revision?.SeRetira.ToArray() ?? [];
    public IReadOnlyList<ElementoRetirada> SeConserva => revision?.SeConserva.ToArray() ?? [];
    public IReadOnlyList<string> Advertencias => revision?.Advertencias ?? [];
    public IReadOnlyList<ProblemaSolicitud> Problemas => revision?.Problemas ?? [];
    public ResultadoRetirada? Resultado { get; private set; }
    public bool Ocupado => ocupado;
    public bool Ejecutando => ejecutando;
    public bool Finalizado => finalizado;
    public bool ProgresoVisible => progresoVisible;
    public bool RevisionVisible => !progresoVisible;
    public bool PuedeRetirar => revision?.Valida == true && !ocupado && !progresoVisible;
    public bool PuedeDetener => ocupado && !cierre.IsCancellationRequested;
    public bool MostrarRegistro => !ocupado && !string.IsNullOrWhiteSpace(ArchivoRegistro);
    public bool MostrarDetalles { get => mostrarDetalles; set => Cambiar(ref mostrarDetalles, value); }
    public PasoInstalacionVisible? PasoActual => pasoActual;
    public string Mensaje => mensaje;
    public string? ArchivoRegistro => Resultado?.ArchivoRegistro;
    public IReadOnlyList<string> Indicaciones => Resultado?.Indicaciones ?? [];
    // La retirada no tiene datos para estaciones; la vista compartida oculta su recuadro con este valor.
    public DatosEstaciones? Estaciones => null;
    public string TituloProgreso => !finalizado ? "Retirando los programas"
        : Resultado?.Terminada == true ? "Retirada completada" : "La retirada necesita atención";
    public string DescripcionProgreso => "La información conservada permanece disponible. Cada paso muestra su comprobación.";

    public async Task RevisarAsync(SolicitudRetirada solicitud)
    {
        if (iniciado || cierre.IsCancellationRequested) return;
        iniciado = true;
        ocupado = true;
        Actualizar();
        try
        {
            if (revisar is null) throw new InvalidOperationException("No se configuró el servicio de retirada.");
            revision = await Task.Run(() => revisar(solicitud, cierre.Token));
            cierre.Token.ThrowIfCancellationRequested();
            mensaje = revision.Valida ? "Revisa lo que se retirará. No se borrarán los datos académicos ni las copias."
                : "No es seguro continuar con esta instalación. Revisa los problemas indicados.";
        }
        catch (OperationCanceledException) when (cierre.IsCancellationRequested)
        { mensaje = "La revisión se canceló. No se solicitó retirar componentes."; }
        catch (Exception error) when (EsFalloPresentable(error))
        { mensaje = "No se pudo comprobar la instalación. No se solicitó retirar componentes."; }
        finally { ocupado = false; Actualizar(); }
    }

    public async Task RetirarAsync(bool confirmado)
    {
        if (!confirmado || !PuedeRetirar || revision is null || retirar is null) return;
        var prevista = revision;
        ocupado = ejecutando = progresoVisible = true;
        mensaje = "Espera mientras se comprueba cada paso.";
        foreach (var paso in prevista.Pasos) Pasos.Add(new(paso.Id, paso.Nombre, paso.Descripcion));
        if (Pasos.LastOrDefault() is { } ultimo) ultimo.Ultimo = true;
        Actualizar();
        try
        {
            var progreso = new Progress<AvanceInstalacion>(avance =>
            {
                if (finalizado) return;
                pasoActual = Pasos.FirstOrDefault(p => p.Id == avance.PasoId);
                pasoActual?.Actualizar(avance.Estado, avance.Detalle);
                Notificar(nameof(PasoActual));
            });
            Resultado = await Task.Run(() => retirar(prevista, progreso, cierre.Token));
            foreach (var paso in Resultado.Pasos)
                Pasos.FirstOrDefault(p => p.Id == paso.Id)?.Actualizar(paso.Estado, paso.Detalle);
            pasoActual = Pasos.FirstOrDefault(p => p.Estado is "Fallido" or "Sin comprobar") ?? Pasos.LastOrDefault(p => p.Estado != "No ejecutado") ?? pasoActual;
            Notificar(nameof(PasoActual));
            mensaje = Resultado.Terminada ? "Consulta las indicaciones y conserva los datos y claves bajo custodia."
                : "No se presenta como completada. Conserva el registro y revisa las indicaciones antes de reintentar.";
        }
        catch (OperationCanceledException) when (cierre.IsCancellationRequested)
        { mensaje = "Se solicitó detener la retirada. Revisa su estado antes de reintentar."; }
        catch (Exception error) when (EsFalloPresentable(error))
        { mensaje = "No se pudo confirmar la retirada. No borres archivos manualmente; revisa el registro de instalación."; }
        finally { ocupado = ejecutando = false; finalizado = true; Actualizar(); }
    }

    public void SolicitarCancelacion()
    {
        if (!ocupado || cierre.IsCancellationRequested) return;
        cierre.Cancel();
        mensaje = ejecutando ? "Se detendrá al terminar el paso actual. No apagues el equipo."
            : "Cancelando la revisión de la instalación.";
        Actualizar();
    }

    private static bool EsFalloPresentable(Exception error) => error is IOException or UnauthorizedAccessException
        or InvalidOperationException or ArgumentException or Win32Exception or System.Security.SecurityException;

    private void Actualizar()
    {
        foreach (var nombre in new[] { nameof(Ocupado), nameof(Ejecutando), nameof(Finalizado), nameof(ProgresoVisible),
            nameof(RevisionVisible), nameof(PuedeRetirar), nameof(PuedeDetener), nameof(MostrarRegistro), nameof(Mensaje),
            nameof(SeRetira), nameof(SeConserva), nameof(Advertencias), nameof(Problemas), nameof(Resultado),
            nameof(ArchivoRegistro), nameof(Indicaciones), nameof(TituloProgreso) }) Notificar(nombre);
    }
}
