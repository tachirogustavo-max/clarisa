using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Domain.Entities;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

internal sealed class RepositorioCierresMariaDb(SesionMariaDb sesion) : IRepositorioCierres
{
    private const int MaximoCierres = 200;
    // Materias por inscripción de una sección completa: mismo límite que las calificaciones por inscripciones.
    private const int MaximoResultados = RepositorioCalificacionesMariaDb.MaximoFilasPorInscripciones;
    private const string Columnas = """
        SELECT id, anio_lectivo_id, seccion_id, numero, politica_id, estado, cerrado_por, cerrado_utc_ticks, anulado_por,
            anulado_utc_ticks, motivo_anulacion, version
        FROM cierres_resultados
        """;
    private const string ColumnasResultado = """
        SELECT inscripcion_id, materia_id, modalidad, tipo_lapso1, valor_lapso1, letra_lapso1, version_lapso1, tipo_lapso2, valor_lapso2,
            letra_lapso2, version_lapso2, tipo_lapso3, valor_lapso3, letra_lapso3, version_lapso3, estado_definitiva, valor_definitiva
        FROM resultados_cerrados
        """;

    public Task<CierreResultados?> ObtenerAsync(Guid id, CancellationToken ct) =>
        sesion.PrimeroAsync(Columnas + " WHERE id = @id", Convertir, ct, Parametros.Id("@id", id));

    public Task<CierreResultados?> VigenteAsync(Guid seccionId, CancellationToken ct) =>
        sesion.PrimeroAsync(Columnas + " WHERE seccion_id = @seccion AND estado = 1", Convertir, ct, Parametros.Id("@seccion", seccionId));

    public Task<IReadOnlyList<CierreResultados>> ListarAsync(Guid seccionId, CancellationToken ct) =>
        sesion.ListarAsync(Columnas + " WHERE seccion_id = @seccion ORDER BY numero DESC", Convertir, MaximoCierres, ct,
            Parametros.Id("@seccion", seccionId));

    public async Task<int> SiguienteNumeroAsync(Guid seccionId, CancellationToken ct) =>
        checked((int)await sesion.EnteroAsync("SELECT COALESCE(MAX(numero), 0) + 1 FROM cierres_resultados WHERE seccion_id = @seccion", ct,
            Parametros.Id("@seccion", seccionId)));

    public Task<IReadOnlyList<ResultadoCerrado>> ResultadosAsync(Guid cierreId, IReadOnlyCollection<Guid> inscripcionIds, CancellationToken ct) =>
        ConsultaPorIds.ListarAsync(sesion, ColumnasResultado + " WHERE cierre_id = @cierre AND inscripcion_id IN ({0}) ORDER BY id",
            inscripcionIds, ConvertirResultado, ct, MaximoResultados, Parametros.Id("@cierre", cierreId));

    public async Task AgregarAsync(CierreResultados cierre, IReadOnlyList<ResultadoCerrado> resultados, CancellationToken ct)
    {
        await sesion.EjecutarAsync("""
            INSERT INTO cierres_resultados (id, anio_lectivo_id, seccion_id, numero, politica_id, estado, cerrado_por, cerrado_utc_ticks,
                anulado_por, anulado_utc_ticks, motivo_anulacion, version)
            VALUES (@id, @anio, @seccion, @numero, @politica, @estado, @cerrado_por, @cerrado_en, @anulado_por, @anulado_en, @motivo, @version)
            """, ct, Datos(cierre));
        foreach (var resultado in resultados)
            await sesion.EjecutarAsync("""
                INSERT INTO resultados_cerrados (cierre_id, inscripcion_id, materia_id, modalidad, tipo_lapso1, valor_lapso1, letra_lapso1,
                    version_lapso1, tipo_lapso2, valor_lapso2, letra_lapso2, version_lapso2, tipo_lapso3, valor_lapso3, letra_lapso3,
                    version_lapso3, estado_definitiva, valor_definitiva)
                VALUES (@cierre, @inscripcion, @materia, @modalidad, @tipo1, @valor1, @letra1, @version1, @tipo2, @valor2, @letra2, @version2,
                    @tipo3, @valor3, @letra3, @version3, @estado_def, @valor_def)
                """, ct, DatosResultado(cierre.Id, resultado));
    }

    public async Task<long> ActualizarAsync(CierreResultados cierre, CancellationToken ct)
    {
        var filas = await sesion.EjecutarAsync("""
            UPDATE cierres_resultados SET estado = @estado, anulado_por = @anulado_por, anulado_utc_ticks = @anulado_en,
                motivo_anulacion = @motivo, version = version + 1
            WHERE id = @id AND seccion_id = @seccion AND version = @version
            """, ct, Datos(cierre));
        await sesion.ExigirUnaFilaAsync(filas, "cierres_resultados", cierre.Id, ct);
        return cierre.Version + 1;
    }

    private static Parametro[] Datos(CierreResultados c) =>
    [
        Parametros.Id("@id", c.Id), Parametros.Id("@anio", c.AnioLectivoId), Parametros.Id("@seccion", c.SeccionId),
        Parametros.Entero("@numero", c.Numero), Parametros.Id("@politica", c.PoliticaId), Parametros.Entero("@estado", (int)c.Estado),
        Parametros.Id("@cerrado_por", c.Traza.CerradoPor), Parametros.Instante("@cerrado_en", c.Traza.CerradoEn),
        Parametros.IdOpcional("@anulado_por", c.Traza.AnuladoPor), Parametros.InstanteOpcional("@anulado_en", c.Traza.AnuladoEn),
        Parametros.Texto("@motivo", c.Traza.MotivoAnulacion), Parametros.Largo("@version", c.Version)
    ];

    private static Parametro[] DatosResultado(Guid cierreId, ResultadoCerrado resultado)
    {
        var lapsos = resultado.Lapsos.ToDictionary(l => l.Orden);
        var parametros = new List<Parametro>
        {
            Parametros.Id("@cierre", cierreId), Parametros.Id("@inscripcion", resultado.InscripcionId),
            Parametros.Id("@materia", resultado.MateriaId), Parametros.EnteroOpcional("@modalidad", (int?)resultado.Modalidad),
            Parametros.Entero("@estado_def", (int)resultado.Definitiva.Estado), Parametros.Nota("@valor_def", resultado.Definitiva.Valor)
        };
        for (var orden = 1; orden <= PoliticaAcademica.LapsosDelAnio; orden++)
        {
            var lapso = lapsos[orden];
            parametros.Add(Parametros.Entero($"@tipo{orden}", (int)lapso.Tipo));
            parametros.Add(Parametros.Nota($"@valor{orden}", lapso.Valor));
            parametros.Add(Parametros.Texto($"@letra{orden}", lapso.Letra));
            parametros.Add(Parametros.Largo($"@version{orden}", lapso.VersionOrigen));
        }
        return parametros.ToArray();
    }

    private static CierreResultados Convertir(MySqlDataReader f) => new(f.Id(0), f.Id(1), f.Id(2), f.GetInt32(3), f.Id(4),
        f.Enumerado<EstadoCierre>(5), new TrazaCierre(f.Id(6), f.Instante(7), f.IdOpcional(8), f.InstanteOpcional(9), f.TextoOpcional(10)),
        f.GetInt64(11));

    private static ResultadoCerrado ConvertirResultado(MySqlDataReader f)
    {
        var lapsos = Enumerable.Range(1, PoliticaAcademica.LapsosDelAnio).Select(orden =>
        {
            var inicio = 3 + (orden - 1) * 4;
            return new LapsoCerrado(orden, f.Enumerado<TipoResultado>(inicio), f.Nota(inicio + 1), f.TextoOpcional(inicio + 2),
                f.GetInt64(inicio + 3));
        }).ToList();
        ModalidadEvaluacion? modalidad = f.IsDBNull(2) ? null : f.Enumerado<ModalidadEvaluacion>(2);
        return new ResultadoCerrado(f.Id(0), f.Id(1), modalidad, lapsos, new Definitiva(f.Enumerado<EstadoDefinitiva>(15), f.Nota(16)));
    }
}
