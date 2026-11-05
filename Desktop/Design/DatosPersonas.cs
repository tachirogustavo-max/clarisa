using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Design;
public sealed class DatosPersonas : ModeloPersonas
{
    public void MostrarRechazoDeCedula()
    {
        NuevoRegistro();
        Formulario!.Cedula = "12345678";
        Formulario.Nombres = "Ana María";
        Formulario.Apellidos = "Mendoza Rojas";
        Formulario.Validar();
        ErrorFormulario = "La cédula ya está registrada. Revisa el registro existente antes de crear otro.";
        Formulario.AplicarErrorServidor("Identificacion", "La cédula ya está registrada.");
        ActualizarEditor();
    }
    public DatosPersonas() : this(false)
    {
    }

    public DatosPersonas(bool docentes) : base(docentes)
    {
        IncluirInactivos = true;
        DatosDiseno([new(Guid.Parse("00000000-0000-0000-0000-000000000001"), "12345678", "Ana María", "Mendoza Rojas", new(2012, 4, 12), "", true, 1), new(Guid.Parse("00000000-0000-0000-0000-000000000002"), null, "Luis Andrés", "Pérez", null, "", true, 1), new(Guid.Parse("00000000-0000-0000-0000-000000000003"), "E-8765432", "Valeria", "Romero Díaz", new(2011, 9, 20), "", true, 2), new(Guid.Parse("00000000-0000-0000-0000-000000000004"), "23456789", "José Antonio", "Torres Salas", null, "", false, 3)]);
    }
}
