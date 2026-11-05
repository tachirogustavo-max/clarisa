using Clarisa.Desktop.Presentation.Forms;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Models;
public class ModeloPersonas : ModeloCatalogo<PersonaRemota, FormularioPersona>
{
    public ModeloPersonas(bool docentes = false, IClienteCatalogo<PersonaRemota>? cliente = null) : base(cliente)
    {
        EsDocente = docentes;
        Titulo = docentes ? "Docentes" : "Estudiantes";
        Descripcion = docentes ? "Registro del personal docente y sus datos de contacto." : "Datos de los estudiantes y su organización académica.";
        TextoNuevo = docentes ? "Registrar docente" : "Registrar estudiante";
    }

    public bool EsDocente { get; }

    protected override FormularioPersona CrearFormulario(PersonaRemota? registro) => new(EsDocente, registro);
}
