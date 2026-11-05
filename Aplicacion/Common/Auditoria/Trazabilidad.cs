using Clarisa.Domain.Entities;

namespace Clarisa.Application.Common.Auditoria;

public enum AccionVentanaCarga
{
    Apertura = 1,
    CambioPlazo = 2,
    Cierre = 3
}

public sealed record MovimientoVentanaCarga(Guid VentanaId, Guid LapsoId, AccionVentanaCarga Accion, DateTimeOffset? Hasta,
    long VersionResultante, Guid ActorUsuarioId, DateTimeOffset OcurridoUtc);

// Las propiedades de estado quedan nulas solo en el historial anterior a los estados académicos (migración 0004).
public sealed record CambioCalificacion(Guid CalificacionId, Guid InscripcionId, Guid MateriaId, Guid LapsoId, decimal? Anterior,
    decimal? Nuevo, long VersionResultante, Guid ActorUsuarioId, DateTimeOffset OcurridoUtc)
{
    public TipoResultado? TipoAnterior { get; init; }
    public TipoResultado? TipoNuevo { get; init; }
    public string? LetraAnterior { get; init; }
    public string? LetraNueva { get; init; }
    public string? Motivo { get; init; }
    public int? InasistenciasAnteriores { get; init; }
    public int? InasistenciasNuevas { get; init; }
}
