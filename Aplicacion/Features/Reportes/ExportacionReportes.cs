using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Application.Features.LecturasAcademicas;

namespace Clarisa.Application.Features.Reportes;

public sealed class ExportacionReportes(IFabricaLecturasAcademicas fabrica, IGeneradorPdfReportes generador, IReloj reloj)
{
    public const int MaximoFilas = 2000;
    public async Task<byte[]> GenerarAsync(Actor actor, FiltroReporte filtro, CancellationToken ct)
    {
        filtro = ConsultaReportes.ValidarFiltro(actor, filtro);
        var filas = new List<FilaReporte>();
        DateTimeOffset consultadoEn;
        await using (var unidad = await fabrica.IniciarAsync(ct))
        {
            var alcance = await AccesoLecturas.ResolverAsync(actor, unidad, ct);
            var primera = await unidad.ReportesAsync(alcance, filtro, new(1, 200), ct);
            if (primera.Total > MaximoFilas) throw Error.EstadoNoPermitido("El informe supera 2000 registros. Filtra por año, sección o materia antes de exportar.");
            filas.AddRange(primera.Elementos);
            for (var numero = 2; filas.Count < primera.Total; numero++)
            {
                var pagina = await unidad.ReportesAsync(alcance, filtro, new(numero, 200), ct);
                if (pagina.Total != primera.Total || pagina.Elementos.Count == 0)
                    throw Error.EstadoNoPermitido("No se pudo reunir una copia consistente del informe. Vuelve a consultar.");
                filas.AddRange(pagina.Elementos);
            }
            var decimales = await unidad.DecimalesCapturaAsync(filtro.AnioLectivoId!.Value, ct);
            filas = filas.Select(fila => fila.Con(decimales)).ToList();
            consultadoEn = reloj.Ahora;
        }
        return await generador.GenerarAsync(new(consultadoEn, filas, filtro.Texto), ct);
    }
}
