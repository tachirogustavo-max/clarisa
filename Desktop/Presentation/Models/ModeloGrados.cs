using Clarisa.Desktop.Presentation.Forms;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Models;
public class ModeloGrados : ModeloCatalogo<GradoRemoto, FormularioGrado>
{
    public ModeloGrados(IClienteCatalogo<GradoRemoto>? cliente = null) : base(cliente)
    {
        Titulo = "Años de estudio";
        Descripcion = "Los años que ofrece el liceo. Las secciones se crean dentro de un año activo.";
        TextoNuevo = "Crear año";
    }

    protected override FormularioGrado CrearFormulario(GradoRemoto? registro) =>
        new(registro, Elementos.Count == 0 ? 1 : Math.Min(Elementos.Max(g => g.Orden) + 1, 20));
}
