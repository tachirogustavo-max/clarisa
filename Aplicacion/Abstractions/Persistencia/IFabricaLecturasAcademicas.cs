using Clarisa.Application.Common.Consultas;
using Clarisa.Application.Features.LecturasAcademicas;
using Clarisa.Application.Features.Reportes;

namespace Clarisa.Application.Abstractions.Persistencia;

public interface IFabricaLecturasAcademicas
{
    Task<IUnidadLecturasAcademicas> IniciarAsync(CancellationToken ct);
}

public interface IUnidadLecturasAcademicas : IAsyncDisposable
{
    Task<bool> DocenteActivoAsync(Guid docenteId, CancellationToken ct);
    Task<Pagina<InscripcionVista>> InscripcionesAsync(FiltroLecturas filtro, ConsultaPagina pagina, CancellationToken ct);
    Task<Pagina<AsignacionVista>> AsignacionesAsync(AlcanceAcademico alcance, FiltroLecturas filtro, ConsultaPagina pagina, CancellationToken ct);
    Task<IReadOnlyList<OpcionAcademica>> OpcionesAsync(TipoCatalogoAcademico tipo, AlcanceAcademico alcance, Guid? anioId, Guid? seccionId, int limite, CancellationToken ct);
    Task<int?> DecimalesCapturaAsync(Guid anioLectivoId, CancellationToken ct);
    Task<Pagina<FilaReporte>> ReportesAsync(AlcanceAcademico alcance, FiltroReporte filtro, ConsultaPagina pagina, CancellationToken ct);
}
