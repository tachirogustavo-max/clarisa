using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Design;
public static class DatosEscritorio
{
    public static ModeloEscritorio Crear(ModuloVista modulo, bool administracion = true)
    {
        var sesion = new SesionActual(administracion ? "María Fernanda Pérez" : "Luis Alberto García", administracion ? "Administracion" : "Docente", DateTimeOffset.UtcNow.AddHours(1), administracion ? "m.perez" : "l.garcia");
        ModeloEscritorio? modelo = null;
        object Crear(ModuloVista clave)
        {
            object contenido = clave switch
            {
                ModuloVista.Estudiantes => new ModeloArea([new("Registro", new DatosPersonas()), new("Inscripciones", new DatosInscripciones())]),
                ModuloVista.Docentes => new ModeloArea([new("Registro", new DatosPersonas(true)), new("Asignación de materias", new DatosAsignaciones())]),
                ModuloVista.Materias => new DatosMaterias(),
                ModuloVista.Calificaciones => new DatosMisMaterias(administracion),
                ModuloVista.Inicio when administracion => new ModeloArea([new("Años escolares", new DatosAnios()), new("Momentos", new DatosLapsos()), new("Carga de notas", new DatosVentanas())]),
                ModuloVista.Inicio => new DatosMisMaterias(),
                ModuloVista.Secciones => new DatosSecciones(),
                ModuloVista.Boletines => new ModeloArea([new("Boletines", new DatosBoletines()), new("Firmas del boletín", new DatosFirmantes())]),
                ModuloVista.Reportes => new DatosReportes(administracion),
                ModuloVista.Usuarios => new DatosUsuarios(),
                ModuloVista.Respaldos => new DatosRespaldos(),
                _ => throw new ArgumentOutOfRangeException(nameof(clave))};
            if (contenido is ModeloMisMaterias materias)
                materias.PlanillaLista += (origen, planilla) =>
                {
                    if (ReferenceEquals(modelo?.Contenido, origen))
                        modelo!.AbrirDetalle(ModuloVista.Calificaciones, planilla);
                };
            return contenido;
        }

        modelo = new ModeloEscritorio(sesion, Crear);
        modelo.Abrir(modulo);
        return modelo;
    }
}
