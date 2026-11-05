using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Lapsos;

public sealed record CrearLapso(Guid Id, Guid AnioLectivoId, string? Nombre, int Orden, DateOnly Inicio, DateOnly Fin);

public sealed record ModificarLapso(Guid Id, long VersionEsperada, string? Nombre, int Orden, DateOnly Inicio, DateOnly Fin);

public sealed record SeleccionarLapsoActual(Guid AnioLectivoId, long VersionEsperada, Guid? LapsoId);

public sealed record LapsoDatos(Guid Id, Guid AnioLectivoId, string Nombre, int Orden, DateOnly Inicio, DateOnly Fin, bool EsActual,
    long Version)
{
    public static LapsoDatos Desde(Lapso lapso, Guid? lapsoActualId) => new(lapso.Id, lapso.AnioLectivoId, lapso.Nombre, lapso.Orden,
        lapso.Fechas.Inicio, lapso.Fechas.Fin, lapso.Id == lapsoActualId, lapso.Version);
}
