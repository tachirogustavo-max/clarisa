using Clarisa.Desktop.Presentation.Forms;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Models;
public class ModeloAnios : ModeloCatalogo<AnioRemoto, FormularioAnio>
{
    public override bool MostrarInactivos => false;

    public ModeloAnios(IClienteCatalogo<AnioRemoto>? cliente = null) : base(cliente)
    {
        Titulo = "Años escolares";
        Descripcion = "Calendario académico y configuración de los tres momentos.";
        TextoNuevo = "Crear año escolar";
    }

    protected override FormularioAnio CrearFormulario(AnioRemoto? registro) => new(registro);
}
