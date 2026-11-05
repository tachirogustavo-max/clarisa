namespace Clarisa.Desktop.Services;
public sealed record ContextoPlanilla(Guid SeccionId, Guid MateriaId, Guid LapsoId);
public sealed record NotaParaGuardar(Guid InscripcionId, decimal? Valor, long VersionEsperada)
{
    public string? Letra { get; init; }
    public bool IncluyeInasistencias { get; init; }
    public int? Inasistencias { get; init; }
}
public sealed record NotaConfirmada(Guid InscripcionId, decimal? Valor, long Version)
{
    public int? Tipo { get; init; }
    public string? Letra { get; init; }
    public int? Inasistencias { get; init; }
}
public enum FalloGuardado
{
    Ninguno,
    Conexion,
    VentanaCerrada,
    VersionObsoleta,
    Rechazado
}

public sealed record RespuestaGuardado(IReadOnlyList<NotaConfirmada> Notas, FalloGuardado Fallo, string? Mensaje);
public interface IClientePlanillas
{
    Task<RespuestaGuardado> GuardarAsync(ContextoPlanilla contexto, IReadOnlyList<NotaParaGuardar> notas, CancellationToken ct);
}
