using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Domain.Entities;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

internal sealed class RepositorioFirmantesMariaDb(SesionMariaDb sesion) : IRepositorioFirmantes
{
    private const string Columnas = """
        SELECT id, numero, director, coordinador_evaluacion, coordinador_estudios, motivo, registrado_por, registrado_utc_ticks
        FROM firmantes_boletin
        """;

    public Task<FirmantesBoletin?> VigentesAsync(CancellationToken ct) =>
        sesion.PrimeroAsync(Columnas + " ORDER BY numero DESC", Convertir, ct);

    public Task<FirmantesBoletin?> ObtenerAsync(Guid id, CancellationToken ct) =>
        sesion.PrimeroAsync(Columnas + " WHERE id = @id", Convertir, ct, Parametros.Id("@id", id));

    public Task AgregarAsync(FirmantesBoletin f, CancellationToken ct) =>
        sesion.EjecutarAsync("""
            INSERT INTO firmantes_boletin (id, numero, director, coordinador_evaluacion, coordinador_estudios, motivo, registrado_por,
                registrado_utc_ticks)
            VALUES (@id, @numero, @director, @evaluacion, @estudios, @motivo, @registrado_por, @registrado_en)
            """, ct, Parametros.Id("@id", f.Id), Parametros.Entero("@numero", f.Numero), Parametros.Texto("@director", f.Director),
            Parametros.Texto("@evaluacion", f.CoordinadorEvaluacion), Parametros.Texto("@estudios", f.CoordinadorEstudios),
            Parametros.Texto("@motivo", f.Motivo), Parametros.Id("@registrado_por", f.RegistradoPor),
            Parametros.Instante("@registrado_en", f.RegistradoEn));

    private static FirmantesBoletin Convertir(MySqlDataReader f) => new(f.Id(0), f.GetInt32(1), f.TextoOpcional(2), f.TextoOpcional(3),
        f.TextoOpcional(4), f.GetString(5), f.Id(6), f.Instante(7));
}
