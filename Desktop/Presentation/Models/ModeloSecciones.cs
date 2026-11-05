using Clarisa.Desktop.Presentation.Forms;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Models;
public class ModeloSecciones : ModeloCatalogo<SeccionRemota, FormularioSeccion>
{
    public override bool RequiereAnio => true;
    public override bool MostrarInactivos => false;

    public ModeloSecciones(IClienteCatalogo<SeccionRemota>? cliente = null) : base(cliente)
    {
        Titulo = "Secciones";
        Descripcion = "Organiza los grupos por año escolar, grado y sección.";
        TextoNuevo = "Crear sección";
    }

    public IReadOnlyList<OpcionRemota> Anios { get; private set; } = [];

    public void EstablecerAnios(IEnumerable<OpcionRemota> anios)
    {
        Anios = anios.ToArray();
        EstablecerOpcionesAnio(Anios);
        Notificar(nameof(Anios));
    }

    public IReadOnlyList<string> Grados { get; private set; } = [];

    public void EstablecerGrados(IEnumerable<string> grados) => Grados = grados.ToArray();

    protected override FormularioSeccion CrearFormulario(SeccionRemota? registro) => new(Anios, Grados, registro);
}
