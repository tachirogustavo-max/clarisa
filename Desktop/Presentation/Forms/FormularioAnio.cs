using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Forms;
public sealed class FormularioAnio : FormularioRegistro
{
    private string nombre;
    private DateTime? inicio;
    private DateTime? fin;
    public FormularioAnio(AnioRemoto? anio = null)
    {
        Id = anio?.Id ?? Guid.CreateVersion7();
        Version = anio?.Version ?? 0;
        nombre = anio?.Nombre ?? "";
        inicio = anio?.Inicio.ToDateTime(TimeOnly.MinValue);
        fin = anio?.Fin.ToDateTime(TimeOnly.MinValue);
        Inicializar();
    }

    public string Nombre { get => nombre; set => Cambiar(ref nombre, value ?? ""); }
    public DateTime? Inicio { get => inicio; set => Cambiar(ref inicio, value); }
    public DateTime? Fin { get => fin; set => Cambiar(ref fin, value); }
    protected override object EstadoActual => (nombre, inicio, fin);
    protected override IReadOnlyList<string> Campos => [nameof(Nombre), nameof(Inicio), nameof(Fin)];

    protected override string ErrorCampo(string campo) => campo switch
    {
        nameof(Nombre) => Requerido(nombre, "el nombre del año escolar", 40),
        nameof(Inicio) => inicio is null ? "Selecciona la fecha de inicio." : "",
        nameof(Fin) => fin is null ? "Selecciona la fecha de finalización." : fin <= inicio ? "La finalización debe ser posterior al inicio." : "",
        _ => ""
    };
    public override object CrearSolicitud()
    {
        if (inicio is null || fin is null)
            throw new InvalidOperationException("El formulario no está completo.");
        var desde = DateOnly.FromDateTime(inicio.Value);
        var hasta = DateOnly.FromDateTime(fin.Value);
        return Nuevo ? new
        {
            Id,
            Nombre,
            Inicio = desde,
            Fin = hasta
        }

        : new
        {
            Id,
            VersionEsperada = Version,
            Nombre,
            Inicio = desde,
            Fin = hasta
        };
    }
}
