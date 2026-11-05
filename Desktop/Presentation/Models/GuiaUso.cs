namespace Clarisa.Desktop.Presentation.Models;
public sealed record PasoGuia(string Titulo, string Texto);
public static class GuiaUso
{
    public static IReadOnlyList<PasoGuia> Crear(bool administracion, ModuloVista modulo)
    {
        var pasos = new List<PasoGuia>();
        if (administracion)
        {
            pasos.Add(new("Prepara el año escolar", "Crea el año, sus tres momentos, materias y secciones. Después inscribe a los estudiantes y asigna docentes a sus materias y secciones."));
            pasos.Add(new("Autoriza la carga", "Elegir el momento actual no abre la carga de notas. En Año escolar, entra a Carga de notas para habilitar o cerrar la ventana de cada momento."));
        }
        else
        {
            pasos.Add(new("Encuentra tu grupo", "En Mis materias verás únicamente las materias y secciones que te asignó la dirección. Selecciona el grupo y el momento antes de abrir la planilla."));
            pasos.Add(new("Carga tus notas", "Puedes guardar durante una ventana habilitada por la dirección. Si la carga está cerrada, consulta con dirección; cambiar la fecha del equipo no la habilita."));
        }
        pasos.Add(modulo switch
        {
            ModuloVista.Reportes => new("Consulta y exporta", "Selecciona un año escolar, aplica los filtros y consulta. El PDF refleja esos filtros. Si cambias uno, consulta nuevamente antes de exportar."),
            ModuloVista.Respaldos => new("Conserva una copia fuera del servidor", "Crear respaldo inicia un trabajo: comprueba su estado antes de guardar la copia. La clave de recuperación debe conservarse en un lugar separado. Cómo recuperar explica el mantenimiento local."),
            ModuloVista.Estudiantes => new("Registra e inscribe", "Registro conserva los datos personales; Inscripciones vincula a cada estudiante con su sección y año. La Cédula puede dejarse vacía si el estudiante todavía no tiene."),
            ModuloVista.Docentes => new("Asigna el acceso correcto", "Registrar al docente, asignarle materias y crear su cuenta son acciones distintas. Revisa la sección y el año antes de asignar una materia."),
            ModuloVista.Usuarios => new("Protege las cuentas", "Cada persona debe tener su propia cuenta. El administrador puede restablecer contraseñas; cada usuario puede cambiar la suya desde el menú de su cuenta, abajo a la izquierda."),
            _ => new("Distingue pendiente de cero", "Una nota pendiente indica que no está registrada; no equivale a cero. La escala de carga es de 1 a 20. Revisa los mensajes junto a cada dato antes de guardar.")
        });
        pasos.Add(new("Si se corta la conexión", "Conserva la pantalla y revisa el mensaje. Si no se confirmó un guardado, vuelve a consultar antes de repetirlo. No cierres una planilla con cambios sin guardarlos o descartarlos conscientemente."));
        pasos.Add(new("Tu cuenta", "Pulsa tu nombre, abajo a la izquierda, para cambiar tu contraseña o cerrar sesión. Cerrar sesión protege tu acceso cuando termines de trabajar en un equipo compartido."));
        return pasos;
    }
}
