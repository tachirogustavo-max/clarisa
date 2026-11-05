using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Consultas;
using Clarisa.Application.Features.LecturasAcademicas;
using Clarisa.Application.Features.Reportes;
using Clarisa.Infrastructure.Persistence.Lecturas;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence;

internal sealed class UnidadLecturasAcademicasMariaDb : UnidadMariaDb, IUnidadLecturasAcademicas
{
    private readonly ConsultaRelacionesMariaDb relaciones;
    private readonly ConsultaOpcionesMariaDb opciones;
    private readonly ConsultaReportesMariaDb reportes;
    public UnidadLecturasAcademicasMariaDb(MySqlConnection conexion, MySqlTransaction transaccion, int segundos)
        : base(conexion, transaccion, ModoUnidadTrabajo.Lectura, segundos)
    {
        relaciones = new(Sesion);
        opciones = new(Sesion);
        reportes = new(Sesion);
    }
    public Task<bool> DocenteActivoAsync(Guid docenteId, CancellationToken ct) =>
        Sesion.ExisteAsync("SELECT 1 FROM docentes WHERE id = @id AND activo = 1", ct, Parametros.Id("@id", docenteId));
    public Task<Pagina<InscripcionVista>> InscripcionesAsync(FiltroLecturas filtro, ConsultaPagina pagina, CancellationToken ct) => relaciones.InscripcionesAsync(filtro, pagina, ct);
    public Task<Pagina<AsignacionVista>> AsignacionesAsync(AlcanceAcademico alcance, FiltroLecturas filtro, ConsultaPagina pagina, CancellationToken ct) => relaciones.AsignacionesAsync(alcance, filtro, pagina, ct);
    public Task<IReadOnlyList<OpcionAcademica>> OpcionesAsync(TipoCatalogoAcademico tipo, AlcanceAcademico alcance, Guid? anioId, Guid? seccionId, int limite, CancellationToken ct) => opciones.ConsultarAsync(tipo, alcance, anioId, seccionId, limite, ct);
    public async Task<int?> DecimalesCapturaAsync(Guid anioLectivoId, CancellationToken ct)
    {
        var filas = await Sesion.ListarAsync("SELECT decimales_captura FROM politicas_academicas WHERE anio_lectivo_id = @anio AND estado = 2",
            fila => fila.IsDBNull(0) ? (int?)null : fila.GetInt32(0), 1, ct, Parametros.Id("@anio", anioLectivoId));
        return filas.Count == 0 ? null : filas[0];
    }
    public Task<Pagina<FilaReporte>> ReportesAsync(AlcanceAcademico alcance, FiltroReporte filtro, ConsultaPagina pagina, CancellationToken ct) => reportes.ConsultarAsync(alcance, filtro, pagina, ct);
}
