using Clarisa.Application.Features.ConfiguracionAcademica;
using Clarisa.Domain.Common;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Calificaciones;

public sealed record ConsultarPlanilla(Guid SeccionId, Guid MateriaId, Guid LapsoId);

// Valor es solo la nota numérica; el estado del resultado se lee en Tipo para no confundir pendiente, letra y exoneración.
public sealed record FilaPlanilla(Guid InscripcionId, Guid EstudianteId, string? Identificacion, string Nombres, string Apellidos,
    decimal? Valor, long Version)
{
    public TipoResultado Tipo { get; init; }
    public string? Letra { get; init; }
    public string? MotivoExoneracion { get; init; }
    public int? Inasistencias { get; init; }
}

// La escala viaja con la planilla para que el escritorio valide con los mismos límites que el servidor, sin repetirlos.
public sealed record PlanillaCalificaciones(Guid SeccionId, Guid MateriaId, Guid LapsoId, bool CargaPermitida, DateTimeOffset? CargaHasta,
    DateTimeOffset ConsultadaEn, int DecimalesAdmitidos, IReadOnlyList<FilaPlanilla> Filas)
{
    public ModalidadEvaluacion? Modalidad { get; init; }
    public IReadOnlyList<LetraCualitativaDatos> Letras { get; init; } = [];
    public decimal EscalaMinima { get; init; } = EscalaNotas.Minima;
    public decimal EscalaMaxima { get; init; } = EscalaNotas.Maxima;
    public bool ResultadosCerrados { get; init; }
}

// IncluyeInasistencias distingue «sin registrar» de «no enviado»: un cliente anterior no las conoce y no debe borrarlas al guardar.
public sealed record NotaEnLote(Guid InscripcionId, decimal? Valor, long VersionEsperada)
{
    public string? Letra { get; init; }
    public bool IncluyeInasistencias { get; init; }
    public int? Inasistencias { get; init; }
}

public sealed record GuardarCalificaciones(Guid SeccionId, Guid MateriaId, Guid LapsoId, IReadOnlyList<NotaEnLote>? Notas);

public sealed record NotaGuardada(Guid InscripcionId, decimal? Valor, long Version, bool Cambio)
{
    public TipoResultado Tipo { get; init; }
    public string? Letra { get; init; }
    public int? Inasistencias { get; init; }
}

public sealed record ResultadoGuardado(IReadOnlyList<NotaGuardada> Notas, int Cambios);

public sealed record Exonerar(Guid SeccionId, Guid MateriaId, Guid LapsoId, Guid InscripcionId, string? Motivo, long VersionEsperada);

public sealed record RevocarExoneracion(Guid SeccionId, Guid MateriaId, Guid LapsoId, Guid InscripcionId, string? Motivo,
    long VersionEsperada);
