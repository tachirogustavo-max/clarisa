using Clarisa.Desktop.Presentation.Forms;
using Clarisa.Desktop.Services.Catalogos;
using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Presentation.Models;
public class ModeloMaterias : ModeloCatalogo<MateriaRemota, FormularioMateria>
{
    private readonly IClienteMateriaEvaluada? evaluaciones;
    private readonly bool configurarEvaluacion;
    private IReadOnlyList<OpcionRemota> anios = [];
    private readonly IClienteGrados? grados;
    private CancellationTokenSource? consultaGrados;
    public ModeloMaterias(IClienteCatalogo<MateriaRemota>? cliente = null, IClienteMateriaEvaluada? evaluaciones = null, bool configurarEvaluacion = false, IClienteGrados? grados = null) : base(cliente)
    {
        Titulo = "Materias";
        Descripcion = "Asignaturas que pueden asignarse a las secciones.";
        TextoNuevo = "Crear materia";
        this.evaluaciones = evaluaciones;
        this.grados = grados;
        this.configurarEvaluacion = configurarEvaluacion || evaluaciones is not null;
    }

    public void EstablecerAnios(IReadOnlyList<OpcionRemota> opciones) => anios = opciones;
    protected override FormularioMateria CrearFormulario(MateriaRemota? registro) => new(registro, anios, configurarEvaluacion, grados is not null);

    // El año puede venir preseleccionado (único año) sin que la vista emita un cambio; el modelo inicia la consulta al abrir.
    protected override void FormularioAbierto(FormularioMateria formulario)
    {
        if (!formulario.ConfigurarEvaluacion || grados is null) return;
        formulario.PropertyChanged += (origen, cambio) =>
        {
            if (cambio.PropertyName == nameof(FormularioMateria.Anio) && ReferenceEquals(origen, Formulario))
                IniciarConsultaGrados();
        };
        IniciarConsultaGrados();
    }

    private async void IniciarConsultaGrados() => await CargarGradosAsync();
    public async Task CargarGradosAsync()
    {
        consultaGrados?.Cancel();
        if (Formulario is not { } formulario || !formulario.ConfigurarEvaluacion || grados is null) return;
        if (formulario.Anio is not { } anio)
        {
            formulario.EstablecerGrados([], "Selecciona el año para consultar sus grados.");
            return;
        }
        formulario.EstablecerGrados([], "Consultando los grados del año…");
        using var actual = new CancellationTokenSource();
        consultaGrados = actual;
        try
        {
            var respuesta = await grados.ConsultarAsync(anio.Id, actual.Token);
            if (actual.IsCancellationRequested || !ReferenceEquals(Formulario, formulario) || formulario.Anio?.Id != anio.Id) return;
            formulario.EstablecerGrados(respuesta.Datos ?? [], respuesta.Error);
        }
        catch (OperationCanceledException) when (actual.IsCancellationRequested) { }
        finally { if (ReferenceEquals(consultaGrados, actual)) consultaGrados = null; }
    }
    public override void Cancelar()
    {
        consultaGrados?.Cancel();
        base.Cancelar();
    }
    public override Task GuardarFormularioAsync() => Formulario is { ConfigurarEvaluacion: true } && evaluaciones is not null
        ? ProcesarGuardadoAsync((formulario, ct) => evaluaciones.CrearAsync(formulario.CrearSolicitud(), ct))
        : base.GuardarFormularioAsync();
}
