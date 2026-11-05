using Clarisa.Application.Features.Respaldos;
using Clarisa.Infrastructure.Configuration;
using Clarisa.Infrastructure.Respaldos.Claves;
using Clarisa.Infrastructure.Respaldos.Mantenimiento;
using Clarisa.Infrastructure.Respaldos.Operaciones;

namespace Clarisa.Infrastructure.Respaldos.MariaDb;

public sealed class GeneradorCopiaActiva(ConfiguracionMariaDb datos, ConfiguracionRespaldos respaldos,
    AlmacenClaveRespaldos claves, AlmacenCopiasArchivo almacen) : IGeneradorCopias
{
    public Task<CopiaPreparada> PrepararAsync(Guid operacionId, DateTimeOffset creadaUtc, CancellationToken ct)
    {
        var activa = ReferenciaBaseActivaArchivo.BaseParaArranque(respaldos.ArchivoBaseActiva, datos.Base);
        return new GeneradorCopiasMariaDb(datos with { Base = activa }, respaldos, claves, almacen).PrepararAsync(operacionId, creadaUtc, ct);
    }
}
