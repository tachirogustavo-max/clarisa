namespace Clarisa.Desktop.Services.Catalogos;
public sealed record AsignacionRemota(Guid Id, Guid SeccionId, Guid MateriaId, Guid DocenteId, string Seccion, string Materia, string Docente, long Version);
public sealed record InscripcionRemota(Guid Id, Guid EstudianteId, Guid SeccionId, string Estudiante, string? Cedula, string Seccion, DateOnly Fecha, string Estado, DateOnly? FechaRetiro, long Version);
public sealed record LapsoRemoto(Guid Id, Guid AnioLectivoId, string Nombre, int Orden, DateOnly Inicio, DateOnly Fin, bool EsActual, long Version);
