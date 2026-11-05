using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.PoliticasAcademicas;

// Todos los valores son opcionales al crear o editar un borrador: lo que falte queda pendiente y se lista en Faltantes.
public sealed record ReglasPoliticaDatos(int? DecimalesCaptura, int? DecimalesResultado, decimal? NotaAprobatoria, FormulaDefinitiva? Formula,
    ModoRedondeo? Redondeo, TratamientoExonerado? Exonerado, decimal? PesoLapso1, decimal? PesoLapso2, decimal? PesoLapso3)
{
    public ReglasPolitica AReglas() => new(DecimalesCaptura, DecimalesResultado, NotaAprobatoria, Formula, Redondeo, Exonerado, PesoLapso1,
        PesoLapso2, PesoLapso3);

    public static ReglasPoliticaDatos Desde(ReglasPolitica r) => new(r.DecimalesCaptura, r.DecimalesResultado, r.NotaAprobatoria, r.Formula,
        r.Redondeo, r.Exonerado, r.PesoLapso1, r.PesoLapso2, r.PesoLapso3);
}

public sealed record PoliticaAcademicaDatos(Guid Id, Guid AnioLectivoId, int Numero, EstadoPolitica Estado, ReglasPoliticaDatos Reglas,
    string Motivo, IReadOnlyList<string> Faltantes, DateTimeOffset CreadaEn, DateTimeOffset? ActivadaEn, string? MotivoActivacion, long Version)
{
    public bool Completa => Faltantes.Count == 0;

    public static PoliticaAcademicaDatos Desde(PoliticaAcademica p) => new(p.Id, p.AnioLectivoId, p.Numero, p.Estado,
        ReglasPoliticaDatos.Desde(p.Reglas), p.Motivo, p.Faltantes, p.Traza.CreadaEn, p.Traza.ActivadaEn, p.Traza.MotivoActivacion, p.Version);
}

// BasadaEn copia las reglas de otra versión del mismo año (normalmente la vigente) como punto de partida del borrador.
public sealed record CrearPolitica(Guid Id, Guid AnioLectivoId, Guid? BasadaEn, string? Motivo, ReglasPoliticaDatos? Reglas);

public sealed record EditarPolitica(Guid Id, long VersionEsperada, string? Motivo, ReglasPoliticaDatos? Reglas);

public sealed record ActivarPolitica(Guid Id, long VersionEsperada, string? Motivo);
