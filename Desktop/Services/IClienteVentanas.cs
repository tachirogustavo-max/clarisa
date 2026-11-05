namespace Clarisa.Desktop.Services;
public sealed record EstadoVentanaRemota(Guid LapsoId, bool PermiteCarga, long Version);
public sealed record RespuestaVentana(EstadoVentanaRemota? Datos, string? Error);
public interface IClienteVentanas
{
    Task<RespuestaVentana> CambiarAsync(Guid lapsoId, bool abrir, long versionEsperada, CancellationToken ct);
    Task<RespuestaVentana> ConsultarAsync(Guid lapsoId, CancellationToken ct);
}
