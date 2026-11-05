using Clarisa.Desktop.Presentation.Forms;
using Clarisa.Desktop.Services;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Models;

public class ModeloEscalaAnual : ModeloCatalogo<LetraRemota, FormularioLetra>
{
    public ModeloEscalaAnual() : this(null) { }
    public ModeloEscalaAnual(IClienteCatalogo<LetraRemota>? cliente) : base(cliente)
    {
        Titulo = "Escala cualitativa";
        Descripcion = "Letras autorizadas para este año, sin equivalencias numéricas inventadas.";
        TextoNuevo = "Agregar letra";
        TextoEditar = "Retirar letra seleccionada";
    }
    public override bool RequiereAnio => true;
    public override bool MostrarInactivos => false;
    public override string TituloFormulario => Formulario?.Nuevo != false ? "Agregar letra a la escala" : "Retirar letra de la escala";
    public override string TextoGuardarFormulario => Formulario?.Nuevo != false ? "Agregar letra" : "Retirar letra";
    protected override FormularioLetra CrearFormulario(LetraRemota? registro) => new(registro, AnioLectivoId);
}

public class ModeloPlanEstudios : ModeloCatalogo<PlanMateriaRemota, FormularioPlanMateria>
{
    public ModeloPlanEstudios() : this(null) { }
    public ModeloPlanEstudios(IClienteCatalogo<PlanMateriaRemota>? cliente) : base(cliente)
    {
        Titulo = "Evaluación por grado";
        Descripcion = "Una materia puede usar distintas evaluaciones según el grado y el año.";
        TextoNuevo = "Configurar materia";
        PropertyChanged += (_, e) => { if (e.PropertyName == nameof(AnioSeleccionado)) InvalidarPreparacion(); };
    }
    public override bool RequiereAnio => true;
    public override bool MostrarInactivos => false;
    public IReadOnlyList<OpcionRemota> Materias { get; set; } = [];
    public IReadOnlyList<string> Grados { get; set; } = [];
    protected override FormularioPlanMateria CrearFormulario(PlanMateriaRemota? registro) => new(registro, AnioLectivoId, Materias, Grados);
}
