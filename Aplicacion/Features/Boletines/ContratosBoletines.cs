using Clarisa.Domain.Common;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Boletines;

public sealed record ConsultarBoletin(Guid AnioLectivoId, Guid SeccionId, Guid EstudianteId, Guid? LapsoId);

public enum EstadoResultadoBoletin
{
    Pendiente = 0,
    Numerico = 1,
    Cualitativo = 2,
    NumericoYCualitativo = 3,
    Exonerado = 4,
    NoCursada = 5
}

// Valor es solo la nota numérica: letra, exoneración y no cursada se leen en Estado y nunca se promedian. Texto es el formato
// abreviado del servidor, igual en PDF y escritorio.
public sealed record NotaLapsoBoletin(Guid LapsoId, int Orden, decimal? Valor)
{
    public EstadoResultadoBoletin Estado { get; init; }
    public string? Letra { get; init; }
    public string Texto { get; init; } = "";
    public int? Inasistencias { get; init; }
}

public sealed record DefinitivaBoletin(EstadoDefinitiva Estado, decimal? Valor, string Texto);

public sealed record MateriaBoletin(Guid MateriaId, string Codigo, string Nombre, IReadOnlyList<NotaLapsoBoletin> Notas)
{
    public ModalidadEvaluacion? Modalidad { get; init; }
    public DefinitivaBoletin? Definitiva { get; init; }
}

public sealed record LapsoBoletin(Guid LapsoId, int Orden, string Nombre);

// Fila PROMEDIO del boletín: uno por momento (LapsoId) y, con resultados cerrados, otro de las definitivas (LapsoId nulo).
// Inasistencias es la suma de las de todas las materias en ese momento, o en el año para las definitivas; nula si no hay registro.
public sealed record PromedioBoletin(Guid? LapsoId, int? Orden, EstadoDefinitiva Estado, decimal? Valor, string Texto, int? Inasistencias);

// Sin política vigente completa ResultadoDefinitivoDisponible es falso y Pendientes explica qué falta.
public sealed record PoliticaBoletin(decimal EscalaMinima, decimal EscalaMaxima, int? DecimalesAdmitidos, decimal? NotaAprobatoria,
    bool ResultadoDefinitivoDisponible, IReadOnlyList<string> Pendientes)
{
    public Guid? PoliticaId { get; init; }
    public int? NumeroPolitica { get; init; }
    public int? DecimalesResultado { get; init; }

    public static PoliticaBoletin Desde(PoliticaAcademica? politica) => politica is null
        ? new(EscalaNotas.Minima, EscalaNotas.Maxima, null, null, false, ["Política académica del año sin activar"])
        : new(EscalaNotas.Minima, EscalaNotas.Maxima, politica.Reglas.DecimalesCaptura, politica.Reglas.NotaAprobatoria, politica.Completa,
            politica.Faltantes)
        {
            PoliticaId = politica.Id, NumeroPolitica = politica.Numero, DecimalesResultado = politica.Reglas.DecimalesResultado
        };
}

// Presente solo cuando los datos salen de un cierre vigente; sin cierre, el boletín es provisional.
public sealed record CierreAplicadoBoletin(Guid CierreId, int Numero, DateTimeOffset CerradoEn);

public sealed record BoletinDatos(Guid InscripcionId, Guid EstudianteId, string? Identificacion, string Nombres, string Apellidos,
    Guid SeccionId, string Grado, string Seccion, Guid AnioLectivoId, string AnioLectivo, EstadoInscripcion EstadoInscripcion,
    IReadOnlyList<LapsoBoletin> Lapsos, IReadOnlyList<MateriaBoletin> Materias, PoliticaBoletin Politica)
{
    public CierreAplicadoBoletin? Cierre { get; init; }
    public IReadOnlyList<PromedioBoletin> Promedios { get; init; } = [];
}
