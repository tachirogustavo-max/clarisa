namespace Clarisa.Application.Features.Reportes;

public sealed record FiltroReporte(Guid? AnioLectivoId, Guid? SeccionId, Guid? EstudianteId, Guid? LapsoId,
    Guid? MateriaId, Guid? DocenteId, string? Texto);
public sealed record FilaReporte(string Estudiante, string? Cedula, string Docente, string Materia, string Seccion, string Lapso, decimal? Nota, string AnioLectivo = "")
{
    public int? Tipo { get; init; }
    public string? Letra { get; init; }
    // Texto lo fija el caso de uso con los decimales de la política del año; sin él se usa el formato sin decimales fijos.
    public string? Texto { get; init; }
    public string Resultado => Texto ?? Con(null).Texto!;

    public FilaReporte Con(int? decimales) =>
        this with { Texto = Boletines.FormatoResultado.Texto(Boletines.FormatoResultado.Estado(Tipo, Nota), Nota, Letra, decimales) };
}
