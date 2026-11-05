using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Forms;
public sealed class FormularioGrado : FormularioRegistro
{
    private string nombre;
    private int? orden;
    private bool activo;
    public FormularioGrado(GradoRemoto? grado, int siguienteOrden)
    {
        Id = grado?.Id ?? Guid.CreateVersion7();
        Version = grado?.Version ?? 0;
        nombre = grado?.Nombre ?? "";
        orden = grado?.Orden ?? siguienteOrden;
        activo = grado?.Activo ?? true;
        Ordenes = Enumerable.Range(1, 20).Append(orden.Value).Distinct().Order().ToArray();
        Inicializar();
    }

    public IReadOnlyList<int> Ordenes { get; }
    public string Nombre { get => nombre; set => Cambiar(ref nombre, value ?? ""); }
    public int? Orden { get => orden; set => Cambiar(ref orden, value); }
    public bool Activo { get => activo; set => Cambiar(ref activo, value); }
    protected override object EstadoActual => (nombre, orden, activo);
    protected override IReadOnlyList<string> Campos => [nameof(Nombre), nameof(Orden)];

    protected override string ErrorCampo(string campo) => campo switch
    {
        nameof(Nombre) => Requerido(nombre, "el nombre del año", 40),
        nameof(Orden) => orden is null ? "Elige la posición en la lista." : "",
        _ => ""
    };

    public override object CrearSolicitud()
    {
        if (orden is null)
            throw new InvalidOperationException("Falta la posición.");
        return Nuevo ? new { Id, Nombre, Orden = orden.Value } : new { Id, VersionEsperada = Version, Nombre, Orden = orden.Value, Activo };
    }
}
