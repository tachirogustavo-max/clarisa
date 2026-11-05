using System.Text.Json.Serialization;

namespace Clarisa.Desktop.Services;
public sealed record FilaPlanillaRemota(Guid InscripcionId, Guid EstudianteId, [property: JsonPropertyName("identificacion")] string? Cedula, string Nombres, string Apellidos, decimal? Valor, long Version)
{
    public int? Tipo { get; init; }
    public string? Letra { get; init; }
    public string? MotivoExoneracion { get; init; }
    public int? Inasistencias { get; init; }
}
public sealed record LetraPlanillaRemota(Guid Id, Guid AnioLectivoId, string Letra, string? Descripcion, long Version);
public sealed record PlanillaRemota(Guid SeccionId, Guid MateriaId, Guid LapsoId, bool CargaPermitida, int DecimalesAdmitidos, IReadOnlyList<FilaPlanillaRemota> Filas)
{
    public int? Modalidad { get; init; }
    public IReadOnlyList<LetraPlanillaRemota> Letras { get; init; } = [];
    public decimal EscalaMinima { get; init; } = EscalaRemota.Minima;
    public decimal EscalaMaxima { get; init; } = EscalaRemota.Maxima;
    public bool ResultadosCerrados { get; init; }
}
public sealed record LapsoCargaRemoto(Guid Id, string Nombre, int Orden, bool Actual, bool PermiteCarga, long VersionVentana);
