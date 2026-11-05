using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Materias;

public sealed record CrearMateria(Guid Id, string? Codigo, string? Nombre);

public sealed record ModificarMateria(Guid Id, long VersionEsperada, string? Codigo, string? Nombre, bool Activa);

public sealed record MateriaDatos(Guid Id, string Codigo, string Nombre, bool Activa, long Version)
{
    public static MateriaDatos Desde(Materia materia) => new(materia.Id, materia.Codigo, materia.Nombre, materia.Activa, materia.Version);
}
