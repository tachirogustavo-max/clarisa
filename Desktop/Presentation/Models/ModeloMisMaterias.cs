using Clarisa.Desktop.Services;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Models;
public class ModeloMisMaterias : ModeloListado<AsignacionRemota>
{
    private readonly IClienteOpciones? opciones;
    private readonly IClienteLecturas? lecturas;
    private readonly IClientePlanillas? guardados;
    private readonly IClienteExoneraciones? exoneraciones;
    private readonly CancellationTokenSource cierrePlanilla = new();
    private OpcionRemota? anio;
    private OpcionRemota? lapso;
    private long revisionContexto;
    public override bool RequiereAnio => true;
    public ModeloMisMaterias(bool administracion, IClienteCatalogo<AsignacionRemota>? lista = null, IClienteOpciones? opciones = null, IClienteLecturas? lecturas = null, IClientePlanillas? guardados = null, IClienteExoneraciones? exoneraciones = null) : base(lista)
    {
        this.opciones = opciones;
        this.lecturas = lecturas;
        this.guardados = guardados;
        this.exoneraciones = administracion ? exoneraciones : null;
        Titulo = administracion ? "Calificaciones" : "Mis materias";
        Descripcion = "Selecciona la materia, la sección y el momento para abrir su planilla.";
    }

    public event Action<ModeloMisMaterias, ModeloPlanilla>? PlanillaLista;
    public IReadOnlyList<OpcionRemota> Anios { get; private set; } = [];
    public IReadOnlyList<OpcionRemota> Lapsos { get; private set; } = [];

    public OpcionRemota? Anio
    {
        get => anio;
        set
        {
            if (!Establecer(ref anio, value)) return;
            revisionContexto++;
            AnioLectivoId = value?.Id;
        }
    }

    public OpcionRemota? Lapso { get => lapso; set { if (Establecer(ref lapso, value)) revisionContexto++; } }
    public bool Abriendo { get; private set; }

    public async Task IniciarAsync()
    {
        try
        {
            if (opciones is not null)
            {
                var respuesta = await opciones.ConsultarAsync(TipoOpciones.Anios, null, null, cierrePlanilla.Token);
                Anios = respuesta.Datos ?? [];
                Mensaje = respuesta.Error ?? "";
                Notificar(nameof(Anios));
                if (Anio is null && Anios.Count == 1)
                {
                    Anio = Anios[0];
                    await CambiarAnioAsync();
                    return;
                }
            }

            await CargarAsync();
            Actualizar();
        }
        catch (OperationCanceledException)when (cierrePlanilla.IsCancellationRequested)
        {
        }
    }

    public void EstablecerOpciones(IReadOnlyList<OpcionRemota> anios, IReadOnlyList<OpcionRemota> lapsos)
    {
        Anios = anios;
        Lapsos = lapsos;
        Notificar(nameof(Anios));
        Notificar(nameof(Lapsos));
    }

    public async Task CambiarAnioAsync()
    {
        Lapso = null;
        Lapsos = [];
        Notificar(nameof(Lapso));
        Notificar(nameof(Lapsos));
        try
        {
            if (opciones is not null && anio is not null)
            {
                var seleccion = anio.Id;
                var respuesta = await opciones.ConsultarAsync(TipoOpciones.Lapsos, seleccion, null, cierrePlanilla.Token);
                if (anio?.Id != seleccion)
                    return;
                Lapsos = respuesta.Datos ?? [];
                Mensaje = respuesta.Error
                    ?? (Lapsos.Count == 0 ? "Este año escolar no tiene momentos. Créalos en Año escolar → Momentos para poder abrir las planillas." : "");
                Notificar(nameof(Lapsos));
                if (Lapsos.Count == 1)
                {
                    Lapso = Lapsos[0];
                    Notificar(nameof(Lapso));
                }
            }

            await BuscarAsync();
        }
        catch (OperationCanceledException)when (cierrePlanilla.IsCancellationRequested)
        {
        }
    }

    public async Task AbrirAsync(AsignacionRemota asignacion)
    {
        if (Abriendo || cierrePlanilla.IsCancellationRequested)
            return;
        if (Lapso is null)
        {
            Mensaje = Lapsos.Count == 0
                ? "Este año escolar no tiene momentos. Créalos en Año escolar → Momentos para poder abrir las planillas."
                : "Elige el momento en la lista de arriba para abrir la planilla.";
            Actualizar();
            return;
        }

        if (!Elementos.Contains(asignacion) || lecturas is null || guardados is null)
            return;
        var lapso = Lapso;
        var revision = revisionContexto;
        Abriendo = true;
        Notificar(nameof(Abriendo));
        try
        {
            var respuesta = await lecturas.ConsultarAsync<PlanillaRemota>("api/calificaciones?seccionId=" + asignacion.SeccionId + "&materiaId=" + asignacion.MateriaId + "&lapsoId=" + lapso.Id, cierrePlanilla.Token);
            if (revision != revisionContexto) return;
            var contexto = new ContextoPlanilla(asignacion.SeccionId, asignacion.MateriaId, lapso.Id);
            if (!ValidacionPlanillaRemota.EsValida(respuesta.Datos, contexto))
            {
                Mensaje = respuesta.Error ?? "La planilla no llegó completa. No se abrió una copia editable.";
                return;
            }

            var datos = respuesta.Datos!;
            var filas = datos.Filas.Select(f => FabricaFilasNota.Crear(datos, f));
            var modelo = new ModeloPlanilla(guardados, new(datos.SeccionId, datos.MateriaId, datos.LapsoId), filas, datos.CargaPermitida, lecturas, exoneraciones)
            {
                Materia = asignacion.Materia,
                Seccion = asignacion.Seccion,
                Lapso = lapso.Nombre
            };
            PlanillaLista?.Invoke(this, modelo);
        }
        catch (OperationCanceledException)when (cierrePlanilla.IsCancellationRequested)
        {
        }
        finally
        {
            Abriendo = false;
            Notificar(nameof(Abriendo));
            Actualizar();
        }
    }

    public override void Cancelar()
    {
        cierrePlanilla.Cancel();
        base.Cancelar();
    }
}
