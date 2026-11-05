using Clarisa.Desktop.Services.Catalogos;
namespace Clarisa.Desktop.Services;
public interface IClienteGrados
{
    Task<ResultadoRemoto<IReadOnlyList<string>>> ConsultarAsync(Guid anio, CancellationToken ct);
}
public sealed class ClienteGrados(IClienteLecturas lecturas) : IClienteGrados
{
    public async Task<ResultadoRemoto<IReadOnlyList<string>>> ConsultarAsync(Guid anio, CancellationToken ct)
    {
        var respuesta = await lecturas.ConsultarAsync<IReadOnlyList<string>>($"api/anios-lectivos/{anio}/grados", ct);
        if (respuesta.Datos is { } datos && (datos.Count > 100 || datos.Any(g => string.IsNullOrWhiteSpace(g) || g.Length > 40)
            || datos.Distinct(StringComparer.OrdinalIgnoreCase).Count() != datos.Count))
            return new(null, "Los grados recibidos no son válidos. Vuelve a consultar el año escolar.");
        return respuesta;
    }
}
