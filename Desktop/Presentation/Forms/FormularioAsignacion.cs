using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Forms;
public sealed class FormularioAsignacion : FormularioRegistro
{
    private OpcionRemota? seccion;
    private OpcionRemota? materia;
    private OpcionRemota? docente;
    private readonly Guid? docenteOriginal;
    public FormularioAsignacion(IReadOnlyList<OpcionRemota> secciones, IReadOnlyList<OpcionRemota> materias, IReadOnlyList<OpcionRemota> docentes, AsignacionRemota? asignacion = null)
    {
        if (asignacion is not null && materias.All(m => m.Id != asignacion.MateriaId))
            materias = materias.Append(new OpcionRemota(asignacion.MateriaId, asignacion.Materia)).ToArray();
        if (asignacion is not null && docentes.All(d => d.Id != asignacion.DocenteId))
            docentes = docentes.Append(new OpcionRemota(asignacion.DocenteId, asignacion.Docente)).ToArray();
        Secciones = secciones;
        Materias = materias;
        Docentes = docentes;
        Id = asignacion?.Id ?? Guid.CreateVersion7();
        Version = asignacion?.Version ?? 0;
        seccion = secciones.FirstOrDefault(s => s.Id == asignacion?.SeccionId);
        materia = materias.FirstOrDefault(m => m.Id == asignacion?.MateriaId);
        docente = docentes.FirstOrDefault(d => d.Id == asignacion?.DocenteId);
        docenteOriginal = docente?.Id;
        Inicializar();
    }

    public IReadOnlyList<OpcionRemota> Secciones { get; }
    public IReadOnlyList<OpcionRemota> Materias { get; }
    public IReadOnlyList<OpcionRemota> Docentes { get; }
    public OpcionRemota? Seccion { get => seccion; set => Cambiar(ref seccion, value); }
    public OpcionRemota? Materia { get => materia; set => Cambiar(ref materia, value); }
    public OpcionRemota? Docente { get => docente; set => Cambiar(ref docente, value); }
    protected override object EstadoActual => (seccion?.Id, materia?.Id, docente?.Id);
    protected override IReadOnlyList<string> Campos => [nameof(Seccion), nameof(Materia), nameof(Docente)];

    protected override string ErrorCampo(string campo) => campo switch
    {
        nameof(Seccion) => seccion is null ? "Selecciona la sección." : "",
        nameof(Materia) => materia is null ? "Selecciona la materia." : "",
        nameof(Docente) => docente is null ? "Selecciona el docente." : !Nuevo && docente.Id == docenteOriginal ? "Selecciona un docente distinto al actual." : "",
        _ => ""
    };
    public override object CrearSolicitud()
    {
        if (seccion is null || materia is null || docente is null)
            throw new InvalidOperationException("La asignación no está completa.");
        return Nuevo ? new
        {
            Id,
            SeccionId = seccion.Id,
            MateriaId = materia.Id,
            DocenteId = docente.Id
        }

        : new
        {
            Id,
            VersionEsperada = Version,
            DocenteId = docente.Id
        };
    }
}
