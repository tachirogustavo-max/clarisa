using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.AniosLectivos;

public sealed record CrearAnioLectivo(Guid Id, string? Nombre, DateOnly Inicio, DateOnly Fin);

public sealed record ModificarAnioLectivo(Guid Id, long VersionEsperada, string? Nombre, DateOnly Inicio, DateOnly Fin);

public sealed record AnioLectivoDatos(Guid Id, string Nombre, DateOnly Inicio, DateOnly Fin, Guid? LapsoActualId, long Version)
{
    public static AnioLectivoDatos Desde(AnioLectivo anio) =>
        new(anio.Id, anio.Nombre, anio.Fechas.Inicio, anio.Fechas.Fin, anio.LapsoActualId, anio.Version);
}
