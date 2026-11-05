using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Forms;
public sealed class FormularioLapso : FormularioRegistro
{
    private OpcionRemota? anio;
    private string nombre;
    private int? orden;
    private DateTime? inicio;
    private DateTime? fin;
    public FormularioLapso(IReadOnlyList<OpcionRemota> anios, LapsoRemoto? lapso = null)
    {
        Anios = anios;
        Id = lapso?.Id ?? Guid.CreateVersion7();
        Version = lapso?.Version ?? 0;
        anio = anios.FirstOrDefault(a => a.Id == lapso?.AnioLectivoId);
        nombre = lapso?.Nombre ?? "";
        orden = lapso?.Orden;
        inicio = lapso?.Inicio.ToDateTime(TimeOnly.MinValue);
        fin = lapso?.Fin.ToDateTime(TimeOnly.MinValue);
        Inicializar();
    }

    public IReadOnlyList<OpcionRemota> Anios { get; }
    public IReadOnlyList<int> Ordenes { get; } = [1, 2, 3];
    public OpcionRemota? Anio { get => anio; set => Cambiar(ref anio, value); }
    public string Nombre { get => nombre; set => Cambiar(ref nombre, value ?? ""); }
    public int? Orden { get => orden; set => Cambiar(ref orden, value); }
    public DateTime? Inicio { get => inicio; set => Cambiar(ref inicio, value); }
    public DateTime? Fin { get => fin; set => Cambiar(ref fin, value); }
    protected override object EstadoActual => (anio?.Id, nombre, orden, inicio, fin);
    protected override IReadOnlyList<string> Campos => [nameof(Anio), nameof(Nombre), nameof(Orden), nameof(Inicio), nameof(Fin)];

    protected override string ErrorCampo(string campo) => campo switch
    {
        nameof(Anio) => anio is null ? "Selecciona el año escolar." : "",
        nameof(Nombre) => Requerido(nombre, "el nombre del momento", 60),
        nameof(Orden) => orden is not (1 or 2 or 3) ? "Selecciona uno de los tres momentos." : "",
        nameof(Inicio) => inicio is null ? "Selecciona la fecha de inicio." : "",
        nameof(Fin) => fin is null ? "Selecciona la fecha de finalización." : fin < inicio ? "La finalización no puede ser anterior al inicio." : "",
        _ => ""
    };
    public override object CrearSolicitud()
    {
        if (anio is null || orden is null || inicio is null || fin is null)
            throw new InvalidOperationException("El momento no está completo.");
        var desde = DateOnly.FromDateTime(inicio.Value);
        var hasta = DateOnly.FromDateTime(fin.Value);
        return Nuevo ? new
        {
            Id,
            AnioLectivoId = anio.Id,
            Nombre,
            Orden = orden.Value,
            Inicio = desde,
            Fin = hasta
        }

        : new
        {
            Id,
            VersionEsperada = Version,
            Nombre,
            Orden = orden.Value,
            Inicio = desde,
            Fin = hasta
        };
    }
}
