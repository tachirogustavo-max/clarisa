using Clarisa.Desktop.Services;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Forms;

public sealed class FormularioLetra : FormularioRegistro
{
    private string letra;
    private string descripcion;
    private readonly Guid? anio;
    public FormularioLetra(LetraRemota? dato, Guid? anio)
    {
        Id = dato?.Id ?? Guid.CreateVersion7();
        Version = dato?.Version ?? 0;
        this.anio = dato?.AnioLectivoId ?? anio;
        letra = dato?.Letra ?? "";
        descripcion = dato?.Descripcion ?? "";
        Inicializar();
    }
    public string Letra { get => letra; set => Cambiar(ref letra, (value ?? "").Trim().ToUpperInvariant()); }
    public string Descripcion { get => descripcion; set => Cambiar(ref descripcion, value ?? ""); }
    public bool Consulta => !Nuevo;
    public string Aviso => Nuevo ? "La letra no se convierte automáticamente en una nota. Define únicamente los valores autorizados por el liceo."
        : "Se retirará esta letra de la escala. Si ya tiene resultados o historial, el servidor impedirá eliminarla.";
    protected override object EstadoActual => (letra, descripcion);
    protected override IReadOnlyList<string> Campos => [nameof(Letra), nameof(Descripcion)];
    protected override string ErrorCampo(string campo) => anio is null ? "Selecciona primero el año escolar en el listado."
        : campo == nameof(Letra) && !ValidacionPlanillaRemota.LetraValida(letra) ? "Usa de 1 a 4 letras de A a Z."
        : campo == nameof(Descripcion) && descripcion.Length > 100 ? "La descripción admite hasta 100 caracteres." : "";
    public override object CrearSolicitud() => Nuevo ? new { Id, AnioLectivoId = anio, Letra, Descripcion }
        : new SolicitudRetirarLetra(Version);
}

public sealed class FormularioPlanMateria : FormularioRegistro
{
    private string grado;
    private OpcionRemota? materia;
    private int modalidad;
    private readonly Guid? anio;
    private readonly bool verificarGrados;
    public FormularioPlanMateria(PlanMateriaRemota? dato, Guid? anio, IReadOnlyList<OpcionRemota> materias, IReadOnlyList<string>? grados = null)
    {
        Id = dato?.Id ?? Guid.CreateVersion7();
        Version = dato?.Version ?? 0;
        this.anio = dato?.AnioLectivoId ?? anio;
        Grados = grados ?? [];
        verificarGrados = grados is not null;
        grado = dato is null ? "" : Grados.FirstOrDefault(g => string.Equals(g, dato.Grado, StringComparison.OrdinalIgnoreCase)) ?? dato.Grado;
        Materias = materias;
        materia = materias.FirstOrDefault(m => m.Id == dato?.MateriaId);
        modalidad = dato?.Modalidad ?? 0;
        Inicializar();
    }
    public IReadOnlyList<OpcionRemota> Materias { get; }
    public IReadOnlyList<string> Grados { get; }
    public string AyudaGrados => Grados.Count == 0 ? "No hay grados disponibles. Crea primero las secciones de este año escolar." : "Se utilizan los grados registrados en las secciones del año.";
    public string Grado { get => grado; set => Cambiar(ref grado, value ?? ""); }
    public OpcionRemota? Materia { get => materia; set => Cambiar(ref materia, value); }
    public int Modalidad { get => modalidad; set { Cambiar(ref modalidad, value); Notificar(nameof(NoSeCursa)); } }
    public bool NoSeCursa { get => modalidad == 1; set { if (value != NoSeCursa) Modalidad = value ? 1 : 0; } }
    public bool IdentidadEditable => Nuevo;
    protected override object EstadoActual => (grado, materia?.Id, modalidad);
    protected override IReadOnlyList<string> Campos => [nameof(Grado), nameof(Materia), nameof(Modalidad)];
    protected override string ErrorCampo(string campo) => anio is null ? "Selecciona primero el año escolar en el listado." : campo switch
    {
        nameof(Grado) => verificarGrados && !Grados.Contains(grado, StringComparer.OrdinalIgnoreCase)
            ? "Selecciona un grado registrado en las secciones del año." : Requerido(grado, "el grado, igual que en las secciones", 40),
        nameof(Materia) => materia is null ? "Selecciona la materia." : "",
        nameof(Modalidad) => modalidad is < 1 or > 4 ? "Selecciona cómo se evalúa o indica que no se cursa." : "",
        _ => ""
    };
    public override object CrearSolicitud() => new { Id, AnioLectivoId = anio, Grado, MateriaId = Materia?.Id, Modalidad, VersionEsperada = Version };
}
