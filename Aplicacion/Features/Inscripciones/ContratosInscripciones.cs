namespace Clarisa.Application.Features.Inscripciones;

public sealed record InscribirEstudiante(Guid Id, Guid EstudianteId, Guid SeccionId, DateOnly Fecha);

public sealed record RetirarInscripcion(Guid Id, long VersionEsperada, DateOnly Fecha);

public sealed record EstudianteEnNomina(Guid InscripcionId, Guid EstudianteId, string? Identificacion, string Nombres, string Apellidos);

public sealed record NominaSeccion(Guid SeccionId, IReadOnlyList<EstudianteEnNomina> Estudiantes);
