using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

internal sealed class RepositorioSesionesMariaDb(SesionMariaDb sesion) : IRepositorioSesiones
{
    // Una sola consulta: sesión, cuenta vigente y estado del docente vinculado, en la misma instantánea.
    private const string ConsultaSesion = """
        SELECT s.id, s.caduca_utc_ticks, s.revocada_utc_ticks IS NOT NULL, IFNULL(d.activo, 0) = 1,
            c.id, c.usuario, c.nombre, c.perfil, c.docente_id, c.huella_clave, c.activo, c.version
        FROM sesiones_usuario s
        JOIN cuentas_usuario c ON c.id = s.cuenta_id
        LEFT JOIN docentes d ON d.id = c.docente_id
        """;

    public Task AgregarAsync(SesionNueva nueva, CancellationToken ct) =>
        sesion.EjecutarAsync("""
            INSERT INTO sesiones_usuario (id, cuenta_id, huella_token, creada_utc_ticks, caduca_utc_ticks)
            VALUES (@id, @cuenta, @huella, @creada, @caduca)
            """, ct,
            Parametros.Id("@id", nueva.Id),
            Parametros.Id("@cuenta", nueva.CuentaId),
            Parametros.Texto("@huella", nueva.HuellaToken),
            Parametros.Instante("@creada", nueva.CreadaUtc),
            Parametros.Instante("@caduca", nueva.CaducaUtc));

    public Task<SesionAlmacenada?> ObtenerPorHuellaAsync(string huellaToken, CancellationToken ct) =>
        sesion.PrimeroAsync(ConsultaSesion + " WHERE s.huella_token = @huella", Convertir, ct, Parametros.Texto("@huella", huellaToken));

    internal Task<SesionAlmacenada?> ObtenerPorIdAsync(Guid id, CancellationToken ct) =>
        sesion.PrimeroAsync(ConsultaSesion + " WHERE s.id = @id", Convertir, ct, Parametros.Id("@id", id));

    public async Task<bool> RevocarAsync(Guid sesionId, DateTimeOffset ahoraUtc, MotivoRevocacion motivo, CancellationToken ct) =>
        await sesion.EjecutarAsync("""
            UPDATE sesiones_usuario SET revocada_utc_ticks = @ahora, motivo_revocacion = @motivo
            WHERE id = @id AND revocada_utc_ticks IS NULL
            """, ct, Parametros.Id("@id", sesionId), Parametros.Instante("@ahora", ahoraUtc),
            Parametros.Entero("@motivo", (int)motivo)) == 1;

    public Task<int> RevocarDeCuentaAsync(Guid cuentaId, DateTimeOffset ahoraUtc, MotivoRevocacion motivo, CancellationToken ct) =>
        sesion.EjecutarAsync("""
            UPDATE sesiones_usuario SET revocada_utc_ticks = @ahora, motivo_revocacion = @motivo
            WHERE cuenta_id = @cuenta AND revocada_utc_ticks IS NULL
            """, ct, Parametros.Id("@cuenta", cuentaId), Parametros.Instante("@ahora", ahoraUtc),
            Parametros.Entero("@motivo", (int)motivo));

    // Conserva las «conservar» sesiones vigentes más recientes; la tabla derivada evita la restricción de LIMIT dentro de IN.
    public Task<int> RevocarExcedentesAsync(Guid cuentaId, int conservar, DateTimeOffset ahoraUtc, CancellationToken ct) =>
        sesion.EjecutarAsync("""
            UPDATE sesiones_usuario SET revocada_utc_ticks = @ahora, motivo_revocacion = @motivo
            WHERE cuenta_id = @cuenta AND revocada_utc_ticks IS NULL AND caduca_utc_ticks > @ahora
              AND id NOT IN (SELECT id FROM (
                  SELECT id FROM sesiones_usuario
                  WHERE cuenta_id = @cuenta AND revocada_utc_ticks IS NULL AND caduca_utc_ticks > @ahora
                  ORDER BY creada_utc_ticks DESC, id DESC LIMIT @conservar) AS recientes)
            """, ct, Parametros.Id("@cuenta", cuentaId), Parametros.Instante("@ahora", ahoraUtc),
            Parametros.Entero("@motivo", (int)MotivoRevocacion.Exceso), Parametros.Entero("@conservar", conservar));

    public Task<int> EliminarVencidasAsync(Guid cuentaId, DateTimeOffset antesDeUtc, CancellationToken ct) =>
        sesion.EjecutarAsync("""
            DELETE FROM sesiones_usuario
            WHERE cuenta_id = @cuenta AND (caduca_utc_ticks < @limite OR revocada_utc_ticks < @limite)
            """, ct, Parametros.Id("@cuenta", cuentaId), Parametros.Instante("@limite", antesDeUtc));

    private static SesionAlmacenada Convertir(MySqlDataReader fila) =>
        new(fila.Id(0), fila.Instante(1), fila.GetInt64(2) == 1, RepositorioCuentasMariaDb.Convertir(fila, 4), fila.GetInt64(3) == 1);
}
