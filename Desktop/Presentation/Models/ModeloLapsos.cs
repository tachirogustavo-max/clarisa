using Clarisa.Desktop.Presentation.Forms;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Models;
public class ModeloLapsos : ModeloCatalogo<LapsoRemoto, FormularioLapso>
{
    public override bool RequiereAnio => true;
    public override bool MostrarInactivos => false;

    public ModeloLapsos(IClienteCatalogo<LapsoRemoto>? cliente = null) : base(cliente)
    {
        Titulo = "Momentos";
        Descripcion = "Los tres momentos del año escolar y sus fechas.";
        TextoNuevo = "Crear momento";
    }

    public IReadOnlyList<OpcionRemota> Anios { get; private set; } = [];

    public void EstablecerAnios(IEnumerable<OpcionRemota> anios)
    {
        Anios = anios.ToArray();
        EstablecerOpcionesAnio(Anios);
    }

    protected override FormularioLapso CrearFormulario(LapsoRemoto? registro) => new(Anios, registro);
}
