using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Features.Cuentas;
using Clarisa.Infrastructure.Persistence.Repositorios;
using Clarisa.Infrastructure.Persistence.Sesion;

namespace Clarisa.Infrastructure.Persistence;

internal sealed class VerificadorSesionEnUnidad(SesionMariaDb datos, ReferenciaSesionActual referencia, IReloj reloj)
{
    private DateTimeOffset? caduca;
    public async Task ExigirAsync(CancellationToken ct)
    {
        var almacenada = await new RepositorioSesionesMariaDb(datos).ObtenerPorIdAsync(referencia.SesionId, ct);
        ValidacionSesionOperacion.Exigir(referencia, almacenada, reloj.Ahora);
        caduca = almacenada!.CaducaUtc;
    }
    public void ExigirVigencia()
    {
        if (caduca is null || reloj.Ahora >= caduca) throw Error.SesionNoValida();
    }
}
