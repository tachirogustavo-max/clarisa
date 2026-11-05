namespace Clarisa.Desktop.Services.Catalogos;
public interface IClienteCatalogo<T>
{
    Task<ResultadoRemoto<PaginaRemota<T>>> ConsultarAsync(ConsultaCatalogo consulta, CancellationToken ct);
    Task<ResultadoRemoto<RegistroConfirmado>> GuardarAsync<TCambio>(Guid id, bool nuevo, TCambio cambio, CancellationToken ct);
}
