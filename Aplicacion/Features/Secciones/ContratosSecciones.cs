using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Secciones;

public sealed record CrearSeccion(Guid Id, Guid AnioLectivoId, string? Grado, string? Nombre);

public sealed record ModificarSeccion(Guid Id, long VersionEsperada, string? Grado, string? Nombre);

public sealed record SeccionDatos(Guid Id, Guid AnioLectivoId, string Grado, string Nombre, long Version)
{
    public static SeccionDatos Desde(Seccion seccion) =>
        new(seccion.Id, seccion.AnioLectivoId, seccion.Grado, seccion.Nombre, seccion.Version);
}
