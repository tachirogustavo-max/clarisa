using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Domain.Entities;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

internal sealed class RepositorioPoliticasMariaDb(SesionMariaDb sesion) : IRepositorioPoliticas
{
    private const int MaximoVersiones = 200;
    private const string Columnas = """
        SELECT id, anio_lectivo_id, numero, estado, decimales_captura, decimales_resultado, nota_aprobatoria, formula, redondeo,
            tratamiento_exonerado, peso_lapso1, peso_lapso2, peso_lapso3, motivo, creada_por, creada_utc_ticks, activada_por,
            activada_utc_ticks, motivo_activacion, version
        FROM politicas_academicas
        """;

    public Task<PoliticaAcademica?> ObtenerAsync(Guid id, CancellationToken ct) =>
        sesion.PrimeroAsync(Columnas + " WHERE id = @id", Convertir, ct, Parametros.Id("@id", id));

    public Task<PoliticaAcademica?> VigenteAsync(Guid anioLectivoId, CancellationToken ct) =>
        sesion.PrimeroAsync(Columnas + " WHERE anio_lectivo_id = @anio AND estado = 2", Convertir, ct, Parametros.Id("@anio", anioLectivoId));

    public Task<IReadOnlyList<PoliticaAcademica>> ListarAsync(Guid anioLectivoId, CancellationToken ct) =>
        sesion.ListarAsync(Columnas + " WHERE anio_lectivo_id = @anio ORDER BY numero DESC", Convertir, MaximoVersiones, ct,
            Parametros.Id("@anio", anioLectivoId));

    public async Task<int> SiguienteNumeroAsync(Guid anioLectivoId, CancellationToken ct) =>
        checked((int)await sesion.EnteroAsync("SELECT COALESCE(MAX(numero), 0) + 1 FROM politicas_academicas WHERE anio_lectivo_id = @anio",
            ct, Parametros.Id("@anio", anioLectivoId)));

    public Task AgregarAsync(PoliticaAcademica politica, CancellationToken ct) =>
        sesion.EjecutarAsync("""
            INSERT INTO politicas_academicas (id, anio_lectivo_id, numero, estado, decimales_captura, decimales_resultado, nota_aprobatoria,
                formula, redondeo, tratamiento_exonerado, peso_lapso1, peso_lapso2, peso_lapso3, motivo, creada_por, creada_utc_ticks,
                activada_por, activada_utc_ticks, motivo_activacion, version)
            VALUES (@id, @anio, @numero, @estado, @dec_captura, @dec_resultado, @aprobatoria, @formula, @redondeo, @exonerado, @peso1, @peso2,
                @peso3, @motivo, @creada_por, @creada_en, @activada_por, @activada_en, @motivo_activacion, @version)
            """, ct, Datos(politica));

    public async Task<long> ActualizarAsync(PoliticaAcademica politica, CancellationToken ct)
    {
        var filas = await sesion.EjecutarAsync("""
            UPDATE politicas_academicas SET estado = @estado, decimales_captura = @dec_captura, decimales_resultado = @dec_resultado,
                nota_aprobatoria = @aprobatoria, formula = @formula, redondeo = @redondeo, tratamiento_exonerado = @exonerado,
                peso_lapso1 = @peso1, peso_lapso2 = @peso2, peso_lapso3 = @peso3, motivo = @motivo, activada_por = @activada_por,
                activada_utc_ticks = @activada_en, motivo_activacion = @motivo_activacion, version = version + 1
            WHERE id = @id AND anio_lectivo_id = @anio AND version = @version
            """, ct, Datos(politica));
        await sesion.ExigirUnaFilaAsync(filas, "politicas_academicas", politica.Id, ct);
        return politica.Version + 1;
    }

    private static Parametro[] Datos(PoliticaAcademica p) =>
    [
        Parametros.Id("@id", p.Id), Parametros.Id("@anio", p.AnioLectivoId), Parametros.Entero("@numero", p.Numero),
        Parametros.Entero("@estado", (int)p.Estado), Parametros.EnteroOpcional("@dec_captura", p.Reglas.DecimalesCaptura),
        Parametros.EnteroOpcional("@dec_resultado", p.Reglas.DecimalesResultado), Parametros.Nota("@aprobatoria", p.Reglas.NotaAprobatoria),
        Parametros.EnteroOpcional("@formula", (int?)p.Reglas.Formula), Parametros.EnteroOpcional("@redondeo", (int?)p.Reglas.Redondeo),
        Parametros.EnteroOpcional("@exonerado", (int?)p.Reglas.Exonerado), Parametros.Nota("@peso1", p.Reglas.PesoLapso1),
        Parametros.Nota("@peso2", p.Reglas.PesoLapso2), Parametros.Nota("@peso3", p.Reglas.PesoLapso3), Parametros.Texto("@motivo", p.Motivo),
        Parametros.Id("@creada_por", p.Traza.CreadaPor), Parametros.Instante("@creada_en", p.Traza.CreadaEn),
        Parametros.IdOpcional("@activada_por", p.Traza.ActivadaPor), Parametros.InstanteOpcional("@activada_en", p.Traza.ActivadaEn),
        Parametros.Texto("@motivo_activacion", p.Traza.MotivoActivacion), Parametros.Largo("@version", p.Version)
    ];

    private static PoliticaAcademica Convertir(MySqlDataReader f) => new(f.Id(0), f.Id(1), f.GetInt32(2), f.Enumerado<EstadoPolitica>(3),
        new ReglasPolitica(EnteroOpcional(f, 4), EnteroOpcional(f, 5), f.Nota(6), Opcional<FormulaDefinitiva>(f, 7), Opcional<ModoRedondeo>(f, 8),
            Opcional<TratamientoExonerado>(f, 9), f.Nota(10), f.Nota(11), f.Nota(12)),
        f.GetString(13), new TrazaPolitica(f.Id(14), f.Instante(15), f.IdOpcional(16), f.InstanteOpcional(17), f.TextoOpcional(18)),
        f.GetInt64(19));

    private static int? EnteroOpcional(MySqlDataReader fila, int indice) => fila.IsDBNull(indice) ? null : fila.GetInt32(indice);

    private static T? Opcional<T>(MySqlDataReader fila, int indice) where T : struct, Enum =>
        fila.IsDBNull(indice) ? null : fila.Enumerado<T>(indice);
}
