using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Estudiantes;

public sealed record CrearEstudiante(Guid Id, string? Identificacion, string? Nombres, string? Apellidos, DateOnly? FechaNacimiento);

public sealed record ModificarEstudiante(Guid Id, long VersionEsperada, string? Identificacion, string? Nombres, string? Apellidos,
    DateOnly? FechaNacimiento, bool Activo);

public sealed record EstudianteDatos(Guid Id, string? Identificacion, string Nombres, string Apellidos, DateOnly? FechaNacimiento,
    bool Activo, long Version)
{
    public static EstudianteDatos Desde(Estudiante estudiante) => new(estudiante.Id, estudiante.Identificacion, estudiante.Nombres,
        estudiante.Apellidos, estudiante.FechaNacimiento, estudiante.Activo, estudiante.Version);
}
