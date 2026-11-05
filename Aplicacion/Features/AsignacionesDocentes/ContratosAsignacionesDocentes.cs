using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.AsignacionesDocentes;

public sealed record AsignarDocente(Guid Id, Guid SeccionId, Guid MateriaId, Guid DocenteId);

public sealed record CambiarDocente(Guid Id, long VersionEsperada, Guid DocenteId);

public sealed record AsignacionDocenteDatos(Guid Id, Guid SeccionId, Guid MateriaId, Guid DocenteId, long Version)
{
    public static AsignacionDocenteDatos Desde(AsignacionDocente asignacion) =>
        new(asignacion.Id, asignacion.SeccionId, asignacion.MateriaId, asignacion.DocenteId, asignacion.Version);
}
