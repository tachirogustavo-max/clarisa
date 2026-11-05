using Clarisa.Instalacion.Contratos;
namespace Clarisa.Setup.Modelos;
public sealed class PasoInstalacionVisible(string id, string nombre, string descripcion) : ModeloObservable
{
    private string estado = "Pendiente";
    private string detalle = descripcion;
    private bool ultimo;
    public bool Ultimo { get => ultimo; set => Cambiar(ref ultimo, value); }
    public string Id { get; } = id;
    public string Nombre { get; } = nombre;
    public string Estado { get => estado; private set => Cambiar(ref estado, value); }
    public string Detalle { get => detalle; private set => Cambiar(ref detalle, value); }
    public void Actualizar(EstadoPaso valor, string mensaje)
    {
        Estado = valor switch { EstadoPaso.EnCurso => "En curso", EstadoPaso.NoComprobado => "Sin comprobar", EstadoPaso.NoEjecutado => "No ejecutado", _ => valor.ToString() };
        Detalle = mensaje;
    }
}
