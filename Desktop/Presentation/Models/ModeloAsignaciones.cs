using Clarisa.Desktop.Presentation.Forms;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Models;
public class ModeloAsignaciones : ModeloCatalogo<AsignacionRemota, FormularioAsignacion>
{
    public override bool RequiereAnio => true;
    public override bool MostrarInactivos => false;

    public ModeloAsignaciones(IClienteCatalogo<AsignacionRemota>? cliente = null) : base(cliente)
    {
        Titulo = "Asignaciones docentes";
        Descripcion = "Relaciona cada materia y sección con el docente que la impartirá.";
        TextoNuevo = "Asignar docente";
    }

    public IReadOnlyList<OpcionRemota> Secciones { get; private set; } = [];
    public IReadOnlyList<OpcionRemota> Materias { get; private set; } = [];
    public IReadOnlyList<OpcionRemota> Docentes { get; private set; } = [];

    public void EstablecerOpciones(IReadOnlyList<OpcionRemota> secciones, IReadOnlyList<OpcionRemota> materias, IReadOnlyList<OpcionRemota> docentes)
    {
        Secciones = secciones;
        Materias = materias;
        Docentes = docentes;
    }

    protected override FormularioAsignacion CrearFormulario(AsignacionRemota? registro) => new(
        Secciones.Where(s => s.AnioLectivoId == AnioLectivoId).ToArray(), Materias, Docentes, registro);
}
