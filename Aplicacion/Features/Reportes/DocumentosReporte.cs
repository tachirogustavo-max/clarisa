namespace Clarisa.Application.Features.Reportes;

public sealed record DocumentoReporte(DateTimeOffset ConsultadoEn, IReadOnlyList<FilaReporte> Filas, string? Busqueda);
public interface IGeneradorPdfReportes
{
    Task<byte[]> GenerarAsync(DocumentoReporte documento, CancellationToken ct);
}
