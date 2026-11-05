using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Grados;

public sealed record CrearGrado(Guid Id, string? Nombre, int Orden);

public sealed record ModificarGrado(Guid Id, long VersionEsperada, string? Nombre, int Orden, bool Activo);

public sealed record GradoDatos(Guid Id, string Nombre, int Orden, bool Activo, long Version)
{
    public static GradoDatos Desde(Grado grado) => new(grado.Id, grado.Nombre, grado.Orden, grado.Activo, grado.Version);
}
