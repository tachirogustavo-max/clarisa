using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Forms;
public sealed class FormularioInscripcion : FormularioRegistro
{
    private OpcionRemota? estudiante;
    private OpcionRemota? seccion;
    private DateTime? fecha;
    private bool confirmarRetiro;
    private readonly DateOnly? fechaInscripcion;
    private readonly bool retirada;
    public FormularioInscripcion(IReadOnlyList<OpcionRemota> estudiantes, IReadOnlyList<OpcionRemota> secciones, InscripcionRemota? inscripcion = null)
    {
        if (inscripcion is not null && estudiantes.All(e => e.Id != inscripcion.EstudianteId))
            estudiantes = estudiantes.Append(new OpcionRemota(inscripcion.EstudianteId, inscripcion.Estudiante)).ToArray();
        Estudiantes = estudiantes;
        Secciones = secciones;
        Id = inscripcion?.Id ?? Guid.CreateVersion7();
        Version = inscripcion?.Version ?? 0;
        estudiante = estudiantes.FirstOrDefault(e => e.Id == inscripcion?.EstudianteId);
        seccion = secciones.FirstOrDefault(s => s.Id == inscripcion?.SeccionId);
        fechaInscripcion = inscripcion?.Fecha;
        retirada = inscripcion?.Estado == "Retirada";
        Inicializar();
    }

    public IReadOnlyList<OpcionRemota> Estudiantes { get; }
    public IReadOnlyList<OpcionRemota> Secciones { get; }
    public OpcionRemota? Estudiante { get => estudiante; set => Cambiar(ref estudiante, value); }
    public OpcionRemota? Seccion { get => seccion; set => Cambiar(ref seccion, value); }
    public DateTime? Fecha { get => fecha; set => Cambiar(ref fecha, value); }
    public bool ConfirmarRetiro { get => confirmarRetiro; set => Cambiar(ref confirmarRetiro, value); }
    public string EtiquetaFecha => Nuevo ? "Fecha de inscripción" : "Fecha de retiro";
    protected override object EstadoActual => (estudiante?.Id, seccion?.Id, fecha, confirmarRetiro);
    protected override IReadOnlyList<string> Campos => [nameof(Estudiante), nameof(Seccion), nameof(Fecha), nameof(ConfirmarRetiro)];

    protected override string ErrorCampo(string campo) => campo switch
    {
        nameof(Estudiante) => estudiante is null ? "Selecciona el estudiante." : "",
        nameof(Seccion) => seccion is null ? "Selecciona la sección." : "",
        nameof(ConfirmarRetiro) => !Nuevo && !confirmarRetiro ? "Confirma que deseas retirar esta inscripción." : "",
        nameof(Fecha) => retirada ? "Esta inscripción ya está retirada." : fecha is null ? "Selecciona la fecha." : !Nuevo && DateOnly.FromDateTime(fecha.Value) < fechaInscripcion ? "El retiro no puede ser anterior a la inscripción." : "",
        _ => ""
    };
    public override object CrearSolicitud()
    {
        if (estudiante is null || seccion is null || fecha is null)
            throw new InvalidOperationException("La inscripción no está completa.");
        var dia = DateOnly.FromDateTime(fecha.Value);
        return Nuevo ? new
        {
            Id,
            EstudianteId = estudiante.Id,
            SeccionId = seccion.Id,
            Fecha = dia
        }

        : new
        {
            Id,
            VersionEsperada = Version,
            Fecha = dia
        };
    }
}
