namespace Clarisa.Application.Features.LecturasAcademicas;

public enum TipoCatalogoAcademico { Anios, Secciones, Estudiantes, Docentes, Materias, Lapsos }
public sealed record OpcionAcademica(Guid Id, string Nombre, Guid? AnioLectivoId = null);
public sealed record AlcanceAcademico(Guid? DocenteId);
public sealed record FiltroLecturas(Guid? AnioLectivoId, Guid? SeccionId, string? Texto);
public sealed record InscripcionVista(Guid Id, Guid EstudianteId, Guid SeccionId, string Estudiante, string? Cedula,
    string Seccion, DateOnly Fecha, string Estado, DateOnly? FechaRetiro, long Version);
public sealed record AsignacionVista(Guid Id, Guid SeccionId, Guid MateriaId, Guid DocenteId,
    string Seccion, string Materia, string Docente, long Version);
