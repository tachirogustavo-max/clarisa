using Clarisa.Desktop.Services;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Models;
public class ModeloVentanas : ModeloObservable, ISalidaProtegida
{
    private readonly IClienteOpciones? opciones;
    private readonly IClienteLecturas? lecturas;
    private readonly IClienteVentanas? ventanas;
    private readonly IClienteCalendario? calendario;
    private long versionAnio;
    private Guid? anioCargado;
    private OpcionRemota? objetivoActual;
    private OpcionRemota? lapsoSeleccionado;
    private readonly CancellationTokenSource cierre = new();
    private OpcionRemota? anio;
    private long revisionConsulta;
    public ModeloVentanas(IClienteOpciones? opciones = null, IClienteLecturas? lecturas = null, IClienteVentanas? ventanas = null, IClienteCalendario? calendario = null)
    {
        this.opciones = opciones;
        this.lecturas = lecturas;
        this.ventanas = ventanas;
        this.calendario = calendario;
    }

    public IReadOnlyList<OpcionRemota> Anios { get; protected set; } = [];
    public OpcionRemota? Anio { get => anio; set => Establecer(ref anio, value); }
    public ModeloDirector? Director { get; protected set; }
    public IReadOnlyList<OpcionRemota> LapsosActuales { get; private set; } = [];

    public OpcionRemota? LapsoSeleccionado
    {
        get => lapsoSeleccionado;
        set
        {
            Establecer(ref lapsoSeleccionado, value);
            Notificar(nameof(PuedeProponerActual));
        }
    }

    public bool PuedeProponerActual => !CambiandoActual && !DebeConsultar && Director?.Ocupado != true && anioCargado == Anio?.Id && LapsoSeleccionado is not null;
    public bool ConfirmacionActualVisible => objetivoActual is not null;
    public string NombreActualPropuesto => objetivoActual?.Nombre ?? "";
    public bool CambiandoActual { get; private set; }
    public bool DebeConsultar { get; private set; }
    public bool FaltaPolitica { get; private set; }
    public string Mensaje { get; private set; } = "Selecciona el año escolar para consultar sus ventanas de carga.";

    public async Task IniciarAsync()
    {
        if (opciones is null || cierre.IsCancellationRequested)
            return;
        try
        {
            var respuesta = await opciones.ConsultarAsync(TipoOpciones.Anios, null, null, cierre.Token);
            Anios = respuesta.Datos ?? [];
            Mensaje = respuesta.Error ?? "Selecciona el año escolar.";
            Notificar(nameof(Anios));
            Notificar(nameof(Mensaje));
        }
        catch (OperationCanceledException)when (cierre.IsCancellationRequested)
        {
        }
    }

    public async Task ConsultarAsync()
    {
        if (anio is null || lecturas is null || Director?.Ocupado == true || CambiandoActual || cierre.IsCancellationRequested)
            return;
        var seleccion = anio;
        var revision = ++revisionConsulta;
        try
        {
            var respuesta = await lecturas.ConsultarAsync<ConfiguracionCargaRemota>("api/anios-lectivos/" + seleccion.Id + "/carga-notas", cierre.Token);
            if (anio?.Id != seleccion.Id || revision != revisionConsulta)
                return;
            var datos = respuesta.Datos?.Lapsos;
            if (respuesta.Datos is not { } configuracion || configuracion.AnioLectivoId != seleccion.Id || configuracion.VersionAnio <= 0 || datos is null || datos.Count > 3 || datos.Any(d => d is null || d.Id == Guid.Empty || d.Orden is < 1 or > 3 || d.VersionVentana < 0) || datos.Select(d => d.Id).Distinct().Count() != datos.Count || datos.Select(d => d.Orden).Distinct().Count() != datos.Count)
            {
                Mensaje = respuesta.Error ?? "No se pudo interpretar el estado de las ventanas.";
                Notificar(nameof(Mensaje));
                return;
            }

            Director?.Cancelar();
            Director = new ModeloDirector(ventanas, datos.OrderBy(d => d.Orden).Select(d => new LapsoVisual(d.Id, d.Nombre, d.Orden, d.Actual, d.PermiteCarga, d.VersionVentana)))
            {
                AnioEscolar = seleccion.Nombre
            };
            Mensaje = "";
            FaltaPolitica = configuracion.PoliticaActiva == false;
            anioCargado = seleccion.Id;
            versionAnio = configuracion.VersionAnio;
            LapsosActuales = datos.OrderBy(d => d.Orden).Select(d => new OpcionRemota(d.Id, d.Nombre)).ToArray();
            LapsoSeleccionado = LapsosActuales.FirstOrDefault(l => datos.Any(d => d.Id == l.Id && d.Actual));
            DebeConsultar = false;
            Notificar(nameof(LapsosActuales));
            Notificar(nameof(LapsoSeleccionado));
            Notificar(nameof(DebeConsultar));
            Notificar(nameof(PuedeProponerActual));
            Notificar(nameof(Director));
            Notificar(nameof(FaltaPolitica));
            Notificar(nameof(Mensaje));
        }
        catch (OperationCanceledException)when (cierre.IsCancellationRequested)
        {
        }
    }

    public bool SolicitarSalida(Action continuar)
    {
        if (CambiandoActual)
            return false;
        objetivoActual = null;
        Notificar(nameof(ConfirmacionActualVisible));
        return Director?.SolicitarSalida(continuar) ?? true;
    }

    public void PedirActual()
    {
        if (CambiandoActual || DebeConsultar || Director?.Ocupado == true || anioCargado != Anio?.Id || LapsoSeleccionado is null || !LapsosActuales.Contains(LapsoSeleccionado))
            return;
        objetivoActual = LapsoSeleccionado;
        Notificar(nameof(ConfirmacionActualVisible));
        Notificar(nameof(NombreActualPropuesto));
    }

    public void CancelarActual()
    {
        if (CambiandoActual)
            return;
        objetivoActual = null;
        Notificar(nameof(ConfirmacionActualVisible));
    }

    public async Task ConfirmarActualAsync()
    {
        if (CambiandoActual || objetivoActual is null || calendario is null || anioCargado is null || cierre.IsCancellationRequested)
            return;
        var anioId = anioCargado.Value;
        CambiandoActual = true;
        Notificar(nameof(CambiandoActual));
        try
        {
            var respuesta = await calendario.SeleccionarActualAsync(anioId, versionAnio, objetivoActual.Id, cierre.Token);
            DebeConsultar = true;
            Mensaje = respuesta.Datos is { } datos && datos.Id == anioId && datos.Version > versionAnio ? "Cambio confirmado. Consulta las ventanas para actualizar la referencia del momento actual." : respuesta.Error ?? "El estado del momento actual queda pendiente de comprobar.";
            objetivoActual = null;
        }
        catch (OperationCanceledException)when (cierre.IsCancellationRequested)
        {
        }
        finally
        {
            CambiandoActual = false;
            foreach (var campo in new[]
            {
                nameof(CambiandoActual),
                nameof(ConfirmacionActualVisible),
                nameof(DebeConsultar),
                nameof(Mensaje),
                nameof(PuedeProponerActual)
            }

            )
                Notificar(campo);
        }
    }

    public void Cancelar()
    {
        cierre.Cancel();
        Director?.Cancelar();
    }
}
