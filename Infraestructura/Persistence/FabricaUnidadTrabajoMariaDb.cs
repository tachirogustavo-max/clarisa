using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Infrastructure.Configuration;

namespace Clarisa.Infrastructure.Persistence;

public sealed class FabricaUnidadTrabajoMariaDb(ConfiguracionMariaDb configuracion) : IFabricaUnidadTrabajo
{
    internal const string ModoSql = AperturaUnidadMariaDb.ModoSql;

    private readonly AperturaUnidadMariaDb apertura = new(configuracion);

    public async Task<IUnidadTrabajo> IniciarAsync(ModoUnidadTrabajo modo, CancellationToken ct) =>
        await apertura.AbrirAsync(modo, (conexion, transaccion, modoUnidad, segundos) =>
            new UnidadTrabajoMariaDb(conexion, transaccion, modoUnidad, segundos), ct);
}
