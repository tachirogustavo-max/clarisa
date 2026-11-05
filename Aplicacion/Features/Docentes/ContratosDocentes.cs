using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Docentes;

public sealed record CrearDocente(Guid Id, string? Identificacion, string? Nombres, string? Apellidos, string? Correo);

public sealed record ModificarDocente(Guid Id, long VersionEsperada, string? Identificacion, string? Nombres, string? Apellidos,
    string? Correo, bool Activo);

public sealed record DocenteDatos(Guid Id, string Identificacion, string Nombres, string Apellidos, string? Correo, bool Activo,
    long Version)
{
    public static DocenteDatos Desde(Docente docente) => new(docente.Id, docente.Identificacion, docente.Nombres, docente.Apellidos,
        docente.Correo, docente.Activo, docente.Version);
}
