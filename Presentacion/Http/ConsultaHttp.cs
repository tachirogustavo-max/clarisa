using Clarisa.Application.Common.Consultas;

namespace Clarisa.Api.Http;

public sealed class ConsultaHttp
{
    public int? Pagina { get; set; }
    public int? Tamanio { get; set; }
    public string? Texto { get; set; }
    public bool? IncluirInactivos { get; set; }
    public Guid? AnioLectivoId { get; set; }
    public ConsultaPagina AConsulta() => ConsultaPagina.Validar(new(Pagina ?? 1, Tamanio ?? 25));
    public FiltroTexto AFiltro() => FiltroTexto.Validar(new(Texto, IncluirInactivos ?? false));
}
