using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Forms;
public sealed class FormularioMateria : FormularioRegistro
{
    private string codigo;
    private string nombre;
    private bool activa;
    private OpcionRemota? anio;
    private string grado = "";
    private int modalidad;
    private readonly bool configurarEvaluacion;
    private readonly bool verificarGrados;
    public FormularioMateria(MateriaRemota? materia = null, IReadOnlyList<OpcionRemota>? anios = null, bool configurarEvaluacion = false, bool verificarGrados = false)
    {
        Id = materia?.Id ?? Guid.CreateVersion7();
        Version = materia?.Version ?? 0;
        codigo = materia?.Codigo ?? "";
        nombre = materia?.Nombre ?? "";
        activa = materia?.Activa ?? true;
        this.configurarEvaluacion = configurarEvaluacion;
        this.verificarGrados = verificarGrados;
        Anios = anios ?? [];
        anio = Anios.Count == 1 ? Anios[0] : null;
        Inicializar();
    }

    public string Codigo { get => codigo; set => Cambiar(ref codigo, value ?? ""); }
    public string Nombre { get => nombre; set => Cambiar(ref nombre, value ?? ""); }
    public bool Activa { get => activa; set => Cambiar(ref activa, value); }
    public IReadOnlyList<OpcionRemota> Anios { get; }
    public Guid PlanId { get; } = Guid.CreateVersion7();
    public bool ConfigurarEvaluacion => configurarEvaluacion && Nuevo;
    public OpcionRemota? Anio
    {
        get => anio;
        set
        {
            if (anio?.Id == value?.Id) return;
            Cambiar(ref anio, value);
            Grado = "";
        }
    }
    public IReadOnlyList<string> Grados { get; private set; } = [];
    public string AyudaGrados { get; private set; } = "Selecciona el año para consultar sus grados.";
    public void EstablecerGrados(IReadOnlyList<string> grados, string? ayuda = null)
    {
        Grados = grados;
        AyudaGrados = ayuda ?? (grados.Count == 0 ? "Este año no tiene grados. Crea primero una sección del grado que vas a configurar." : "Grados registrados en las secciones del año.");
        Notificar(nameof(Grados));
        Notificar(nameof(AyudaGrados));
    }
    public string Grado { get => grado; set => Cambiar(ref grado, value ?? ""); }
    public int Modalidad { get => modalidad; set => Cambiar(ref modalidad, value); }
    protected override object EstadoActual => (codigo, nombre, activa, anio?.Id, grado, modalidad);
    protected override IReadOnlyList<string> Campos => ConfigurarEvaluacion
        ? [nameof(Codigo), nameof(Nombre), nameof(Anio), nameof(Grado), nameof(Modalidad)] : [nameof(Codigo), nameof(Nombre)];

    protected override string ErrorCampo(string campo) => campo switch
    {
        nameof(Codigo) => codigo.Trim().Length is < 1 or > 20 || !codigo.Trim().All(c => char.IsAsciiLetterOrDigit(c) || c == '-') ? "Usa hasta 20 letras, números o guiones." : "",
        nameof(Nombre) => Requerido(nombre, "el nombre de la materia", 100),
        nameof(Anio) => anio is null ? "Selecciona el año escolar de esta evaluación." : "",
        nameof(Grado) => verificarGrados && !Grados.Contains(grado) ? "Selecciona un grado registrado en las secciones del año."
            : Requerido(grado, "el grado, como figura en las secciones", 40),
        nameof(Modalidad) => modalidad is 2 or 3 or 4 ? "" : "Selecciona una forma de evaluación.",
        _ => ""
    };
    public override object CrearSolicitud() => ConfigurarEvaluacion ? new
    {
        Id, Codigo, Nombre, PlanId, AnioLectivoId = Anio?.Id, Grado, Modalidad
    } : Nuevo ? new
    {
        Id,
        Codigo,
        Nombre
    }

    : new
    {
        Id,
        VersionEsperada = Version,
        Codigo,
        Nombre,
        Activa
    };
}
