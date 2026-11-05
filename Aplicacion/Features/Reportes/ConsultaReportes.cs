using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Consultas;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Application.Features.LecturasAcademicas;

namespace Clarisa.Application.Features.Reportes;

public sealed class ConsultaReportes(IFabricaLecturasAcademicas fabrica)
{
    public async Task<Pagina<FilaReporte>> ListarAsync(Actor actor, FiltroReporte filtro, ConsultaPagina pagina, CancellationToken ct)
    {
        filtro = ValidarFiltro(actor, filtro);
        pagina = ConsultaPagina.Validar(pagina);
        await using var unidad = await fabrica.IniciarAsync(ct);
        var alcance = await AccesoLecturas.ResolverAsync(actor, unidad, ct);
        var resultado = await unidad.ReportesAsync(alcance, filtro, pagina, ct);
        var decimales = await unidad.DecimalesCapturaAsync(filtro.AnioLectivoId!.Value, ct);
        return resultado with { Elementos = resultado.Elementos.Select(fila => fila.Con(decimales)).ToList() };
    }
    internal static FiltroReporte ValidarFiltro(Actor actor, FiltroReporte filtro)
    {
        Autorizacion.ExigirPersonal(actor);
        Error.SolicitudRequerida(filtro);
        if (new[] { filtro.AnioLectivoId, filtro.SeccionId, filtro.EstudianteId, filtro.LapsoId, filtro.MateriaId, filtro.DocenteId }.Any(id => id == Guid.Empty))
            throw Error.DatosInvalidos("Uno de los filtros académicos no es válido.");
        if (!Autorizacion.EsAdministracion(actor) && filtro.DocenteId is { } docente && docente != actor.DocenteId)
            throw Error.AccesoDenegado("Solo puedes consultar tus asignaciones.");
        if (filtro.AnioLectivoId is null)
            throw Error.DatosInvalidos("Selecciona el año escolar para consultar el reporte.", nameof(filtro.AnioLectivoId));
        return filtro with { Texto = FiltroTexto.Validar(new(filtro.Texto, false)).Texto };
    }
}
