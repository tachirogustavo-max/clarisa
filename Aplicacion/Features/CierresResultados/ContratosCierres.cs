using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.CierresResultados;

public sealed record CerrarResultados(Guid Id, Guid SeccionId);

public sealed record AnularCierre(Guid Id, long VersionEsperada, string? Motivo);

public sealed record CierreDatos(Guid Id, Guid AnioLectivoId, Guid SeccionId, int Numero, Guid PoliticaId, EstadoCierre Estado,
    DateTimeOffset CerradoEn, DateTimeOffset? AnuladoEn, string? MotivoAnulacion, long Version)
{
    public static CierreDatos Desde(CierreResultados cierre) => new(cierre.Id, cierre.AnioLectivoId, cierre.SeccionId, cierre.Numero,
        cierre.PoliticaId, cierre.Estado, cierre.Traza.CerradoEn, cierre.Traza.AnuladoEn, cierre.Traza.MotivoAnulacion, cierre.Version);
}
