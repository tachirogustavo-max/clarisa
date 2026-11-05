using System.Globalization;

namespace Clarisa.Desktop.Services.Catalogos;
public sealed class ClienteReporteCatalogo(IClienteLecturas lecturas, Func<ConsultaDocumento> filtros, Func<Guid?> materia, Func<Guid?> docente) : IClienteCatalogo<FilaReporte>
{
    public async Task<ResultadoRemoto<PaginaRemota<FilaReporte>>> ConsultarAsync(ConsultaCatalogo consulta, CancellationToken ct)
    {
        var filtro = filtros();
        var ruta = RutasReportes.Crear(new(filtro.AnioLectivoId, filtro.SeccionId, filtro.EstudianteId, filtro.LapsoId, materia(), docente(), consulta.Texto), false, consulta.Pagina, consulta.Tamanio);
        var respuesta = await lecturas.ConsultarAsync<PaginaRemota<FilaReporte>>(ruta, ct);
        if (respuesta.Datos is { } datos && (datos.Elementos is null || datos.Elementos.Any(f => f is null
            || string.IsNullOrWhiteSpace(f.Texto) || !FormatoResultadoRemoto.Coherente(f.Tipo ?? (f.Nota is null ? 0 : 1), f.Nota, f.Letra))))
            return new(null, "El reporte contiene resultados incompletos. No se mostraron como notas pendientes.");
        return respuesta;
    }

    public Task<ResultadoRemoto<RegistroConfirmado>> GuardarAsync<TCambio>(Guid id, bool nuevo, TCambio cambio, CancellationToken ct) => Task.FromResult(new ResultadoRemoto<RegistroConfirmado>(null, "Los reportes son de consulta."));
}
