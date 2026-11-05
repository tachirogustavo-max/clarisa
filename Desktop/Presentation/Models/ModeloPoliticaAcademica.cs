using Clarisa.Desktop.Presentation.Forms;
using Clarisa.Desktop.Services;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Models;

public class ModeloPoliticaAcademica : ModeloObservable, ISalidaProtegida
{
    private readonly IClienteOpciones? opciones;
    private readonly IClientePoliticas? politicas;
    private readonly CancellationTokenSource cierre = new();
    private OpcionRemota? anio;
    private PoliticaRemota? seleccionada;
    private long revision;

    public ModeloPoliticaAcademica(IClienteOpciones? opciones = null, IClientePoliticas? politicas = null)
    {
        this.opciones = opciones;
        this.politicas = politicas;
    }

    public IReadOnlyList<OpcionRemota> Anios { get; protected set; } = [];
    public OpcionRemota? Anio
    {
        get => anio;
        set
        {
            if (!Establecer(ref anio, value)) return;
            Versiones = [];
            Seleccionada = null;
            Notificar(nameof(Versiones));
            Informar("Consulta las versiones de la política de este año.");
        }
    }
    public IReadOnlyList<PoliticaRemota> Versiones { get; protected set; } = [];
    public PoliticaRemota? Seleccionada
    {
        get => seleccionada;
        set
        {
            if (!Establecer(ref seleccionada, value)) return;
            Formulario = value is { Estado: PoliticaRemota.Borrador } borrador ? new FormularioPolitica(borrador) : null;
            MotivoActivacion = "";
            ConfirmacionVisible = false;
            foreach (var propiedad in new[] { nameof(Formulario), nameof(EditandoBorrador), nameof(MotivoActivacion), nameof(ConfirmacionVisible),
                         nameof(Detalle) })
                Notificar(propiedad);
        }
    }
    public FormularioPolitica? Formulario { get; private set; }
    public bool EditandoBorrador => Formulario is not null;
    public string Detalle => seleccionada is null ? "Selecciona una versión para ver sus reglas."
        : $"Versión {seleccionada.Numero} · {seleccionada.EstadoTexto} · {seleccionada.Resumen}"
          + (seleccionada.MotivoActivacion is { } motivo ? $" · Activada por: {motivo}" : "");
    public string MotivoActivacion { get; set; } = "";
    public bool ConfirmacionVisible { get; private set; }
    public bool Ocupado { get; private set; }
    public bool HayVigente => Versiones.Any(v => v.Estado == PoliticaRemota.Vigente);
    public string Mensaje { get; private set; } = "Selecciona el año escolar.";
    public bool MensajeEsError { get; private set; }

    public async Task IniciarAsync()
    {
        if (opciones is null || Anios.Count > 0) return;
        var respuesta = await opciones.ConsultarAsync(TipoOpciones.Anios, null, null, cierre.Token);
        Anios = respuesta.Datos ?? [];
        Notificar(nameof(Anios));
        if (respuesta.Error is { } error) Informar(error, true);
    }

    public Task ConsultarAsync() => EjecutarAsync(async ct =>
    {
        if (anio is not { } elegido || politicas is null) return Informar("Selecciona el año escolar.", true);
        var consulta = ++revision;
        var respuesta = await politicas.ListarAsync(elegido.Id, ct);
        // Una respuesta tardía de otro año no reemplaza la consulta vigente.
        if (consulta != revision || anio?.Id != elegido.Id) return false;
        if (respuesta.Datos is not { } datos) return Informar(respuesta.Error ?? "No se pudo consultar la política.", true);
        MostrarVersiones(datos, seleccionada?.Id);
        return Informar(datos.Count == 0 ? "Este año no tiene política. Crea un borrador y completa sus reglas." : $"{datos.Count} versiones consultadas.");
    });

    public Task CrearBorradorAsync() => EjecutarAsync(async ct =>
    {
        if (anio is not { } elegido || politicas is null) return Informar("Selecciona el año escolar.", true);
        var basada = Versiones.FirstOrDefault(v => v.Estado == PoliticaRemota.Vigente)?.Id;
        var respuesta = await politicas.CrearAsync(Guid.CreateVersion7(), elegido.Id, basada,
            basada is null ? "Primera política del año" : "Cambio a partir de la versión vigente", ct);
        if (respuesta.Datos is not { } creada) return Informar(respuesta.Error ?? "No se creó el borrador.", true);
        MostrarVersiones([creada, .. Versiones], creada.Id);
        return Informar("Borrador creado. Completa sus reglas y guárdalo.");
    });

    public Task GuardarBorradorAsync() => EjecutarAsync(async ct =>
    {
        if (Formulario is not { } formulario || politicas is null) return false;
        if (!formulario.Validar()) return Informar("Revisa los datos marcados.", true);
        var respuesta = await politicas.EditarAsync(formulario.Id, formulario.Version, formulario.Motivo.Trim(),
            (ReglasRemotas)formulario.CrearSolicitud(), ct);
        if (respuesta.Datos is not { } guardada)
        {
            formulario.AplicarErrorServidor(respuesta.Campo, respuesta.Error ?? "");
            return Informar(respuesta.Error ?? "No se guardó el borrador.", true);
        }
        MostrarVersiones(Versiones.Select(v => v.Id == guardada.Id ? guardada : v).ToList(), guardada.Id, forzar: true);
        return Informar(guardada.Faltantes.Count == 0 ? "Borrador guardado y completo: ya puede activarse." : "Borrador guardado. " + guardada.Resumen);
    });

    public void PedirActivacion()
    {
        if (seleccionada is not { Estado: PoliticaRemota.Borrador } || Formulario?.HayCambios == true)
        {
            Informar("Guarda el borrador antes de activarlo.", true);
            return;
        }
        ConfirmacionVisible = true;
        Notificar(nameof(ConfirmacionVisible));
    }

    public void CancelarActivacion()
    {
        ConfirmacionVisible = false;
        Notificar(nameof(ConfirmacionVisible));
    }

    public Task ActivarAsync() => EjecutarAsync(async ct =>
    {
        if (seleccionada is not { } borrador || politicas is null) return false;
        if (string.IsNullOrWhiteSpace(MotivoActivacion)) return Informar("Escribe el motivo de la activación (por ejemplo, el acta).", true);
        var respuesta = await politicas.ActivarAsync(borrador.Id, borrador.Version, MotivoActivacion.Trim(), ct);
        if (respuesta.Datos is null) return Informar(respuesta.Error ?? "No se activó la política.", true);
        CancelarActivacion();
        var actualizada = await politicas.ListarAsync(borrador.AnioLectivoId, ct);
        if (actualizada.Datos is { } datos) MostrarVersiones(datos, borrador.Id, forzar: true);
        return Informar("Política activada. La carga de notas no cambió: ábrela desde Carga de notas cuando corresponda.");
    });

    public bool SolicitarSalida(Action continuar)
    {
        if (Formulario?.HayCambios == true)
        {
            Informar("Hay cambios del borrador sin guardar. Guárdalos o vuelve a consultar antes de salir.", true);
            return false;
        }
        // Una operación en curso termina antes de salir: su respuesta decide qué quedó guardado.
        return !Ocupado;
    }

    protected void MostrarVersiones(IReadOnlyList<PoliticaRemota> datos, Guid? elegir, bool forzar = false)
    {
        Versiones = datos.OrderByDescending(v => v.Numero).ToList();
        Notificar(nameof(Versiones));
        Notificar(nameof(HayVigente));
        if (forzar) seleccionada = null;
        Seleccionada = Versiones.FirstOrDefault(v => v.Id == elegir) ?? Versiones.FirstOrDefault();
    }

    private async Task EjecutarAsync(Func<CancellationToken, Task<bool>> accion)
    {
        if (Ocupado) return;
        Ocupado = true;
        Notificar(nameof(Ocupado));
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
            Notificar(nameof(Ocupado));
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
