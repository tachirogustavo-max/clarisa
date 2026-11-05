using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Errores;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Sesion;

internal enum EstadoSesion
{
    Activa = 1,
    Confirmada = 2,
    Fallida = 3,
    Cerrada = 4
}

// Toda orden usa la conexión y la transacción de la unidad; tras cualquier fallo la unidad ya no admite órdenes ni confirmación.
internal sealed class SesionMariaDb(MySqlConnection conexion, MySqlTransaction transaccion, ModoUnidadTrabajo modo, int segundosComando)
{
    public const int LimiteListas = 2000;

    public ModoUnidadTrabajo Modo { get; } = modo;
    public EstadoSesion Estado { get; private set; } = EstadoSesion.Activa;

    // Tras una cancelación o un fallo técnico no se sabe en qué estado quedó la sesión del servidor: la conexión no vuelve al pool.
    public bool ConexionDudosa { get; private set; }
    public int IdConexion => conexion.ServerThread;
    public MySqlTransaction Transaccion => transaccion;

    public void Marcar(EstadoSesion estado) => Estado = estado;

    public void MarcarDudosa()
    {
        Estado = Estado == EstadoSesion.Cerrada ? Estado : EstadoSesion.Fallida;
        ConexionDudosa = true;
    }

    public void ExigirActiva()
    {
        if (Estado != EstadoSesion.Activa)
            throw new InvalidOperationException($"La unidad de trabajo no admite más órdenes (estado {Estado}).");
    }

    public Task<int> EjecutarAsync(string sql, CancellationToken ct, params Parametro[] parametros)
    {
        if (Modo != ModoUnidadTrabajo.Escritura) throw new InvalidOperationException("Escritura en una unidad de lectura.");
        return ProtegerAsync(sql, parametros, comando => comando.ExecuteNonQueryAsync(ct), ct);
    }

    public Task<long> EnteroAsync(string sql, CancellationToken ct, params Parametro[] parametros) =>
        ProtegerAsync(sql, parametros, async comando =>
        {
            var valor = await comando.ExecuteScalarAsync(ct);
            return valor is null or DBNull ? 0 : Convert.ToInt64(valor, System.Globalization.CultureInfo.InvariantCulture);
        }, ct);

    public async Task<bool> ExisteAsync(string condicionSql, CancellationToken ct, params Parametro[] parametros) =>
        await EnteroAsync("SELECT EXISTS(" + condicionSql + ")", ct, parametros) == 1;

    public Task<T?> PrimeroAsync<T>(string sql, Func<MySqlDataReader, T> convertir, CancellationToken ct,
        params Parametro[] parametros) where T : class => PrimeroAsync(sql, convertir, bloquear: false, ct, parametros);

    // FOR UPDATE va después de LIMIT; solo tiene efecto en unidades de escritura (READ COMMITTED, transacción no de solo lectura).
    public async Task<T?> PrimeroAsync<T>(string sql, Func<MySqlDataReader, T> convertir, bool bloquear, CancellationToken ct,
        params Parametro[] parametros) where T : class =>
        (await LeerAsync(sql + " LIMIT 1" + (bloquear ? " FOR UPDATE" : ""), convertir, 1, ct, parametros)).SingleOrDefault();

    // Las listas sin paginar del contrato tienen un límite técnico: superarlo se informa en lugar de truncar en silencio.
    public async Task<IReadOnlyList<T>> ListarAsync<T>(string sql, Func<MySqlDataReader, T> convertir, CancellationToken ct,
        params Parametro[] parametros) => await ListarAsync(sql, convertir, LimiteListas, ct, parametros);

    public async Task<IReadOnlyList<T>> ListarAsync<T>(string sql, Func<MySqlDataReader, T> convertir, int limite,
        CancellationToken ct, params Parametro[] parametros)
    {
        var filas = await LeerAsync(sql + " LIMIT " + (limite + 1), convertir, limite + 1, ct, parametros);
        if (filas.Count > limite)
            throw Error.EstadoNoPermitido($"La consulta supera el máximo técnico de {limite} registros. Usa filtros más específicos.");
        return filas;
    }

    public Task<List<T>> PaginaAsync<T>(string sqlConLimite, Func<MySqlDataReader, T> convertir, int maximo, CancellationToken ct,
        params Parametro[] parametros) => LeerAsync(sqlConLimite, convertir, maximo, ct, parametros);

    // UPDATE o DELETE condicionado por versión: cero filas significa revisión obsoleta o registro inexistente.
    public async Task ExigirUnaFilaAsync(int filas, string tabla, Guid id, CancellationToken ct)
    {
        if (filas == 1) return;
        if (filas > 1)
        {
            Estado = EstadoSesion.Fallida;
            throw Error.AlmacenNoDisponible(new DatoAlmacenadoInvalidoException($"Se afectaron {filas} filas de {tabla}."));
        }
        var existe = await ExisteAsync($"SELECT 1 FROM {tabla} WHERE id = @id", ct, Parametros.Id("@id", id));
        Estado = EstadoSesion.Fallida;
        throw existe ? Error.VersionObsoleta() : Error.NoEncontrado("El registro ya no existe. Recargue los datos.");
    }

    private async Task<List<T>> LeerAsync<T>(string sql, Func<MySqlDataReader, T> convertir, int maximo, CancellationToken ct,
        Parametro[] parametros) =>
        await ProtegerAsync(sql, parametros, async comando =>
        {
            var filas = new List<T>();
            await using var lector = await comando.ExecuteReaderAsync(ct);
            while (filas.Count < maximo && await lector.ReadAsync(ct))
            {
                try
                {
                    filas.Add(convertir(lector));
                }
                catch (Exception error) when (TraduccionErrores.EsDatoDanado(error))
                {
                    throw Error.AlmacenNoDisponible(error);
                }
            }
            return filas;
        }, ct);

    private async Task<T> ProtegerAsync<T>(string sql, Parametro[] parametros, Func<MySqlCommand, Task<T>> accion, CancellationToken ct)
    {
        ExigirActiva();
        try
        {
            await using var comando = new MySqlCommand(sql, conexion, transaccion) { CommandTimeout = segundosComando };
            foreach (var parametro in parametros)
                comando.Parameters.Add(new MySqlParameter(parametro.Nombre, parametro.Tipo) { Value = parametro.Valor ?? DBNull.Value });
            return await accion(comando);
        }
        catch (ErrorAplicacionException)
        {
            Estado = EstadoSesion.Fallida;
            throw;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            MarcarDudosa();
            throw;
        }
        catch (MySqlException error) when (ct.IsCancellationRequested)
        {
            MarcarDudosa();
            throw new OperationCanceledException("La operación se canceló.", error, ct);
        }
        catch (MySqlException error)
        {
            var traducido = TraduccionErrores.Traducir(error);
            if (traducido.Tipo == TipoError.AlmacenNoDisponible) MarcarDudosa();
            else Estado = EstadoSesion.Fallida;
            throw traducido;
        }
        catch (Exception error) when (TraduccionErrores.EsFalloTecnico(error))
        {
            MarcarDudosa();
            throw Error.AlmacenNoDisponible(error);
        }
    }
}
