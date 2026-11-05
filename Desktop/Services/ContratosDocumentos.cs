using System.Text.Json.Serialization;

namespace Clarisa.Desktop.Services;
public sealed record NotaLapsoRemota(Guid LapsoId, int Orden, decimal? Valor)
{
    public int? Estado { get; init; }
    public string? Letra { get; init; }
    public string? Texto { get; init; }
    public int? Inasistencias { get; init; }
}
public sealed record PromedioRemoto(Guid? LapsoId, int? Orden, string Texto, int? Inasistencias);
public sealed record DefinitivaRemota(int Estado, decimal? Valor, string Texto);
public sealed record CierreRemoto(Guid CierreId, int Numero, DateTimeOffset CerradoEn);
public sealed record MateriaBoletinRemota(Guid MateriaId, string Codigo, string Nombre, IReadOnlyList<NotaLapsoRemota> Notas)
{
    public int? Modalidad { get; init; }
    public DefinitivaRemota? Definitiva { get; init; }
}
public sealed record PoliticaBoletinRemota(bool ResultadoDefinitivoDisponible, IReadOnlyList<string> Pendientes)
{
    public decimal EscalaMinima { get; init; } = EscalaRemota.Minima;
    public decimal EscalaMaxima { get; init; } = EscalaRemota.Maxima;
    public int? NumeroPolitica { get; init; }
}
public sealed record BoletinRemoto(Guid InscripcionId, Guid EstudianteId, [property: JsonPropertyName("identificacion")] string? Cedula, string Nombres, string Apellidos, string Grado, string Seccion, string AnioLectivo, IReadOnlyList<MateriaBoletinRemota> Materias, PoliticaBoletinRemota Politica, Guid SeccionId = default, Guid AnioLectivoId = default)
{
    public CierreRemoto? Cierre { get; init; }
    public IReadOnlyList<PromedioRemoto> Promedios { get; init; } = [];
}
public sealed record FilaBoletin(string Materia, string PrimerLapso, string SegundoLapso, string TercerLapso, string Definitiva = "Pendiente",
    string Inasistencias = "")
{
    public string Revision => EsPromedio ? "" : "Pendiente";
    public bool EsPromedio { get; init; }
}
public sealed record FilaReporte(string Estudiante, string? Cedula, string Docente, string Materia, string Seccion, string Lapso, decimal? Nota, string AnioLectivo = "")
{
    public int? Tipo { get; init; }
    public string? Letra { get; init; }
    public string? Texto { get; init; }
    public string Resultado => Texto ?? FormatoResultadoRemoto.SinVerificar;
}
public sealed record ConsultaDocumento(Guid? AnioLectivoId, Guid? SeccionId, Guid? EstudianteId, Guid? LapsoId);
