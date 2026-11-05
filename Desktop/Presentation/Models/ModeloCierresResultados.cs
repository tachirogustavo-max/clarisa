using Clarisa.Desktop.Services;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Models;

public class ModeloCierresResultados : ModeloObservable, ISalidaProtegida
{
    private readonly IClienteOpciones? opciones;
    private readonly IClientePoliticas? politicas;
    private readonly CancellationTokenSource cierre = new();
    private OpcionRemota? anio;
    private OpcionRemota? seccion;
    private CierreSeccionRemoto? seleccionado;
    private long revision;

    public ModeloCierresResultados(IClienteOpciones? opciones = null, IClientePoliticas? politicas = null)
    {
        this.opciones = opciones;
        this.politicas = politicas;
    }

    public IReadOnlyList<OpcionRemota> Anios { get; protected set; } = [];
    public IReadOnlyList<OpcionRemota> Secciones { get; protected set; } = [];
    public IReadOnlyList<CierreSeccionRemoto> Cierres { get; protected set; } = [];
    public OpcionRemota? Anio
    {
        get => anio;
        set
        {
            if (!Establecer(ref anio, value)) return;
            Seccion = null;
            Secciones = [];
            Notificar(nameof(Secciones));
            _ = CargarSeccionesAsync();
        }
    }
    public OpcionRemota? Seccion
    {
        get => seccion;
        set
        {
            if (!Establecer(ref seccion, value)) return;
            Cierres = [];
            Seleccionado = null;
            Notificar(nameof(Cierres));
            Notificar(nameof(PuedeCerrar));
        }
    }
    public CierreSeccionRemoto? Seleccionado
    {
        get => seleccionado;
        set
        {
            Establecer(ref seleccionado, value);
            Notificar(nameof(PuedeAnular));
        }
    }
    public string MotivoAnulacion { get; set; } = "";
    public bool ConfirmacionCierreVisible { get; private set; }
    public bool Ocupado { get; private set; }
    public bool PuedeCerrar => seccion is not null && !Ocupado && Cierres.All(c => c.Estado != CierreSeccionRemoto.Vigente);
    public bool PuedeAnular => seleccionado is { Estado: CierreSeccionRemoto.Vigente } && !Ocupado;
    public string Mensaje { get; private set; } = "Selecciona el año y la sección.";
    public bool MensajeEsError { get; private set; }

    public async Task IniciarAsync()
    {
        if (opciones is null || Anios.Count > 0) return;
        var respuesta = await opciones.ConsultarAsync(TipoOpciones.Anios, null, null, cierre.Token);
        Anios = respuesta.Datos ?? [];
        Notificar(nameof(Anios));
        if (respuesta.Error is { } error) Informar(error, true);
    }

    private async Task CargarSeccionesAsync()
    {
        if (opciones is null || anio is not { } elegido) return;
        var respuesta = await opciones.ConsultarAsync(TipoOpciones.Secciones, elegido.Id, null, cierre.Token);
        if (anio?.Id != elegido.Id) return;
        Secciones = respuesta.Datos ?? [];
        Notificar(nameof(Secciones));
        if (respuesta.Error is { } error) Informar(error, true);
    }

    public Task ConsultarAsync() => EjecutarAsync(async ct =>
    {
        if (seccion is not { } elegida || politicas is null) return Informar("Selecciona la sección.", true);
        var consulta = ++revision;
        var respuesta = await politicas.CierresAsync(elegida.Id, ct);
        if (consulta != revision || seccion?.Id != elegida.Id) return false;
        if (respuesta.Datos is not { } datos) return Informar(respuesta.Error ?? "No se pudieron consultar los cierres.", true);
        Mostrar(datos);
        return Informar(datos.Count == 0 ? "La sección no tiene cierres: sus resultados son provisionales." : $"{datos.Count} cierres consultados.");
    });

    public void PedirCierre()
    {
        if (!PuedeCerrar) return;
        ConfirmacionCierreVisible = true;
        Notificar(nameof(ConfirmacionCierreVisible));
    }

    public void CancelarCierre()
    {
        ConfirmacionCierreVisible = false;
        Notificar(nameof(ConfirmacionCierreVisible));
    }

    public Task CerrarAsync() => EjecutarAsync(async ct =>
    {
        if (seccion is not { } elegida || politicas is null) return false;
        CancelarCierre();
        var respuesta = await politicas.CerrarAsync(Guid.CreateVersion7(), elegida.Id, ct);
        if (respuesta.Datos is not { } cerrado) return Informar(respuesta.Error ?? "No se cerraron los resultados.", true);
        Mostrar([cerrado, .. Cierres.Where(c => c.Id != cerrado.Id)]);
        return Informar($"Resultados cerrados (cierre n.º {cerrado.Numero}). Los boletines de la sección muestran ahora esta copia fija.");
    });

    public Task AnularAsync() => EjecutarAsync(async ct =>
    {
        if (seleccionado is not { } elegido || politicas is null) return false;
        if (string.IsNullOrWhiteSpace(MotivoAnulacion)) return Informar("Escribe el motivo de la anulación.", true);
        var respuesta = await politicas.AnularAsync(elegido.Id, elegido.Version, MotivoAnulacion.Trim(), ct);
        if (respuesta.Datos is not { } anulado) return Informar(respuesta.Error ?? "No se anuló el cierre.", true);
        MotivoAnulacion = "";
        Notificar(nameof(MotivoAnulacion));
        Mostrar(Cierres.Select(c => c.Id == anulado.Id ? anulado : c).ToList());
        return Informar("Cierre anulado. Su copia se conserva; las notas vuelven a poder corregirse si la carga está abierta.");
    });

    public bool SolicitarSalida(Action continuar) => !Ocupado;

    protected void Mostrar(IReadOnlyList<CierreSeccionRemoto> datos)
    {
        Cierres = datos.OrderByDescending(c => c.Numero).ToList();
        Notificar(nameof(Cierres));
        Notificar(nameof(PuedeCerrar));
        Seleccionado = Cierres.FirstOrDefault(c => c.Estado == CierreSeccionRemoto.Vigente);
    }

    private async Task EjecutarAsync(Func<CancellationToken, Task<bool>> accion)
    {
        if (Ocupado) return;
        Ocupado = true;
        foreach (var propiedad in new[] { nameof(Ocupado), nameof(PuedeCerrar), nameof(PuedeAnular) }) Notificar(propiedad);
        try
        {
            await accion(cierre.Token);
        }
        catch (OperationCanceledException) when (cierre.IsCancellationRequested)
        {
        }
        finally
        {
            Ocupado = false;
            foreach (var propiedad in new[] { nameof(Ocupado), nameof(PuedeCerrar), nameof(PuedeAnular) }) Notificar(propiedad);
        }
    }

    protected bool Informar(string mensaje, bool error = false)
    {
        Mensaje = mensaje;
        MensajeEsError = error;
        Notificar(nameof(Mensaje));
        Notificar(nameof(MensajeEsError));
        return !error;
    }
}
