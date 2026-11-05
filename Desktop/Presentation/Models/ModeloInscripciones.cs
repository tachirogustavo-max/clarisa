using Clarisa.Desktop.Presentation.Forms;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Models;
public class ModeloInscripciones : ModeloCatalogo<InscripcionRemota, FormularioInscripcion>
{
    public override bool RequiereAnio => true;
    public override bool MostrarInactivos => false;

    public ModeloInscripciones(IClienteCatalogo<InscripcionRemota>? cliente = null) : base(cliente)
    {
        Titulo = "Inscripciones";
        Descripcion = "Estudiantes inscritos y retiros por sección.";
        TextoNuevo = "Inscribir estudiante";
        TextoEditar = "Retirar inscripción";
    }

    public override string TituloFormulario => Formulario?.Nuevo == true ? TextoNuevo : "Retirar inscripción";
    public override string TextoGuardarFormulario => Formulario?.Nuevo == true ? "Inscribir" : "Confirmar retiro";
    public IReadOnlyList<OpcionRemota> Estudiantes { get; private set; } = [];
    public IReadOnlyList<OpcionRemota> Secciones { get; private set; } = [];

    public void EstablecerOpciones(IReadOnlyList<OpcionRemota> estudiantes, IReadOnlyList<OpcionRemota> secciones)
    {
        Estudiantes = estudiantes;
        Secciones = secciones;
    }

    protected override FormularioInscripcion CrearFormulario(InscripcionRemota? registro) => new(Estudiantes,
        Secciones.Where(s => s.AnioLectivoId == AnioLectivoId).ToArray(), registro);
}
