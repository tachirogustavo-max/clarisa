namespace Clarisa.Desktop.Presentation.Models;
public enum ModuloVista
{
    Inicio,
    Estudiantes,
    Docentes,
    Materias,
    Secciones,
    Calificaciones,
    Boletines,
    Reportes,
    Usuarios,
    Respaldos
}

public sealed record EntradaNavegacion(ModuloVista Clave, string Nombre, string Icono);
public static class NavegacionPerfil
{
    public static IReadOnlyList<EntradaNavegacion> Crear(bool administracion) => administracion ? new[]
    {
        new EntradaNavegacion(ModuloVista.Inicio, "Año escolar", "M5,5 H19 Q21,5 21,7 V20 Q21,22 19,22 H5 Q3,22 3,20 V7 Q3,5 5,5 Z M7,2 V8 M17,2 V8 M3,11 H21 M7,15 H9 M14,15 H17 M7,18 H9 M14,18 H17"),
        new EntradaNavegacion(ModuloVista.Estudiantes, "Estudiantes", "M2,6 L12,2 L22,6 L12,10 Z M6,8 V12 Q12,16 18,12 V8 M22,6 V12 M8,15 C4,16 3,19 3,22 H21 C21,19 20,16 16,15"),
        new EntradaNavegacion(ModuloVista.Docentes, "Docentes", "M9,3 H22 V16 H14 M13,7 H18 M13,11 H20 M3,10 A3,3 0 1 0 9,10 A3,3 0 1 0 3,10 M2,22 V19 Q6,14 10,19 V22 M16,16 V21 M13,22 H20"),
        new EntradaNavegacion(ModuloVista.Materias, "Materias", "M3,4 Q8,1 12,5 Q16,1 21,4 V20 Q16,17 12,21 Q8,17 3,20 Z M12,5 V21 M6,8 L9,9 M6,12 L9,13 M15,9 L18,8 M15,13 L18,12"),
        new EntradaNavegacion(ModuloVista.Secciones, "Secciones", "M8,2 H16 V8 H8 Z M3,16 H9 V22 H3 Z M15,16 H21 V22 H15 Z M12,8 V12 M6,16 V12 H18 V16"),
        new EntradaNavegacion(ModuloVista.Calificaciones, "Calificaciones", "M8,4 H5 Q3,4 3,6 V21 H21 V6 Q21,4 19,4 H16 M8,2 H16 V6 H8 Z M7,11 L9,13 L12,10 M15,11 H18 M7,17 H12 M15,17 H18"),
        new EntradaNavegacion(ModuloVista.Boletines, "Boletines", "M4,2 H16 L21,7 V21 H4 Z M16,2 V7 H21 M8,7 H12 M8,11 H17 M8,15 H17 M8,18 H13"),
        new EntradaNavegacion(ModuloVista.Reportes, "Reportes", "M3,3 V21 H21 M7,17 V12 H10 V17 M13,17 V7 H16 V17 M19,11 V3 H22 V11"),
        new EntradaNavegacion(ModuloVista.Usuarios, "Usuarios", "M12,2 L21,6 V12 C21,17 17,20 12,23 C7,20 3,17 3,12 V6 Z M9,9 A3,3 0 1 0 15,9 A3,3 0 1 0 9,9 M7,18 Q12,12 17,18"),
        new EntradaNavegacion(ModuloVista.Respaldos, "Respaldos", "M3,6 C3,1 21,1 21,6 C21,11 3,11 3,6 V13 C3,17 12,18 15,16 M21,6 V11 M3,13 V19 Q6,23 13,21 M19,13 V22 M15,18 L19,22 L23,18")
    }

    : new[]
    {
        new EntradaNavegacion(ModuloVista.Inicio, "Mis materias", "M3,4 Q8,1 12,5 Q16,1 21,4 V20 Q16,17 12,21 Q8,17 3,20 Z M12,5 V21 M15,6 V13 L17,11 L19,13 V5"),
        new EntradaNavegacion(ModuloVista.Calificaciones, "Calificaciones", "M4,3 H15 M4,3 V21 H18 V15 M7,8 H12 M7,12 H10 M11,18 L12,14 L19,7 L22,10 L15,17 Z M17,9 L20,12"),
        new EntradaNavegacion(ModuloVista.Reportes, "Mis reportes", "M4,2 H16 L20,6 V13 M4,2 V22 H12 M16,2 V6 H20 M8,7 H11 M8,11 H13 M14,17 A4,4 0 1 0 22,17 A4,4 0 1 0 14,17 M21,20 L24,23")
    };
}
