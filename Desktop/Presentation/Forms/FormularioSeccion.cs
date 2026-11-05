using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Forms;
public sealed class FormularioSeccion : FormularioRegistro
{
    private OpcionRemota? anio;
    private string grado;
    private string nombre;
    public FormularioSeccion(IReadOnlyList<OpcionRemota> anios, IReadOnlyList<string> grados, SeccionRemota? seccion = null)
    {
        Anios = anios;
        Id = seccion?.Id ?? Guid.CreateVersion7();
        Version = seccion?.Version ?? 0;
        anio = anios.FirstOrDefault(a => a.Id == seccion?.AnioLectivoId);
        grado = seccion?.Grado ?? "";
        nombre = seccion?.Nombre ?? "";
        // Una sección de un año ya desactivado conserva su año al editarla.
        Grados = seccion is null || grados.Contains(seccion.Grado) ? grados : [seccion.Grado, .. grados];
        Inicializar();
    }

    public IReadOnlyList<OpcionRemota> Anios { get; }
    // Años de estudio activos; la sección (A, B, C...) se escribe aparte.
    public IReadOnlyList<string> Grados { get; }
    public OpcionRemota? Anio { get => anio; set => Cambiar(ref anio, value); }
    public string Grado { get => grado; set => Cambiar(ref grado, value ?? ""); }
    public string Nombre { get => nombre; set => Cambiar(ref nombre, value ?? ""); }
    protected override object EstadoActual => (anio?.Id, grado, nombre);
    protected override IReadOnlyList<string> Campos => [nameof(Anio), nameof(Grado), nameof(Nombre)];

    protected override string ErrorCampo(string campo) => campo switch
    {
        nameof(Anio) => anio is null ? "Selecciona el año escolar." : "",
        nameof(Grado) => Grados.Contains(grado) ? "" : "Elige el año de la lista.",
        nameof(Nombre) => Requerido(nombre, "el nombre de la sección", 20),
        _ => ""
    };
    public override object CrearSolicitud()
    {
        if (anio is null)
            throw new InvalidOperationException("Falta el año escolar.");
        return Nuevo ? new
        {
            Id,
            AnioLectivoId = anio.Id,
            Grado,
            Nombre
        }

        : new
        {
            Id,
            VersionEsperada = Version,
            Grado,
            Nombre
        };
    }
}
