using System.Data;
using Clarisa.Infrastructure.Respaldos.Formato;
using MySqlConnector;

namespace Clarisa.Infrastructure.Respaldos.MariaDb;

// Inserción parametrizada por lotes, en orden de dependencias y en una sola transacción. Nombres de tabla y columna solo del catálogo.
// El ciclo anios_lectivos ↔ lapsos se resuelve insertando la referencia como NULL y completándola tras cargar lapsos: las claves
// foráneas siguen activas durante toda la carga.
internal static class CargaTablasMariaDb
{
    private const int FilasPorLote = 200;

    public static async Task CargarAsync(MySqlConnection conexion, LectorPaquete paquete, CancellationToken ct)
    {
        await using var transaccion = await conexion.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var diferidas = new List<(TablaRespaldo Tabla, object Id, object Valor)>();
        foreach (var tabla in CatalogoRespaldo.Tablas)
        {
            var indiceDiferida = tabla.Diferida is null ? -1 : tabla.Columnas.Select(c => c.Nombre).ToList().IndexOf(tabla.Diferida);
            if (tabla.Sembrada) await VaciarAsync(conexion, transaccion, tabla, ct);
            var lote = new List<object?[]>(FilasPorLote);
            await paquete.RecorrerAsync(tabla, async valores =>
            {
                if (indiceDiferida >= 0 && valores[indiceDiferida] is { } valor)
                {
                    diferidas.Add((tabla, valores[0]!, valor));
                    valores[indiceDiferida] = null;
                }
                lote.Add(valores);
                if (lote.Count < FilasPorLote) return;
                await InsertarAsync(conexion, transaccion, tabla, lote, ct);
                lote.Clear();
            }, ct);
            if (lote.Count > 0) await InsertarAsync(conexion, transaccion, tabla, lote, ct);
            foreach (var pendiente in diferidas.Where(d => d.Tabla.DespuesDe == tabla.Nombre))
                await CompletarAsync(conexion, transaccion, pendiente.Tabla, pendiente.Id, pendiente.Valor, ct);
        }
        await transaccion.CommitAsync(ct);
    }

    private static async Task VaciarAsync(MySqlConnection conexion, MySqlTransaction transaccion, TablaRespaldo tabla, CancellationToken ct)
    {
        await using var comando = new MySqlCommand($"DELETE FROM `{tabla.Nombre}`", conexion, transaccion) { CommandTimeout = ConexionRespaldo.SegundosOperacion };
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertarAsync(MySqlConnection conexion, MySqlTransaction transaccion, TablaRespaldo tabla,
        List<object?[]> filas, CancellationToken ct)
    {
        var columnas = string.Join(", ", tabla.Columnas.Select(c => $"`{c.Nombre}`"));
        await using var comando = new MySqlCommand { Connection = conexion, Transaction = transaccion, CommandTimeout = ConexionRespaldo.SegundosOperacion };
        var grupos = new List<string>(filas.Count);
        for (var fila = 0; fila < filas.Count; fila++)
        {
            var nombres = new string[tabla.Columnas.Count];
            for (var columna = 0; columna < nombres.Length; columna++)
            {
                nombres[columna] = $"@p{fila}_{columna}";
                comando.Parameters.Add(Parametro(nombres[columna], tabla.Columnas[columna].Tipo, filas[fila][columna]));
            }
            grupos.Add("(" + string.Join(", ", nombres) + ")");
        }
        comando.CommandText = $"INSERT INTO `{tabla.Nombre}` ({columnas}) VALUES {string.Join(", ", grupos)}";
        var insertadas = await comando.ExecuteNonQueryAsync(ct);
        if (insertadas != filas.Count) throw ErroresRespaldo.CopiaDanada($"{tabla.Nombre}: no se insertaron todos los registros.");
    }

    private static async Task CompletarAsync(MySqlConnection conexion, MySqlTransaction transaccion, TablaRespaldo tabla, object id,
        object valor, CancellationToken ct)
    {
        var columnaId = tabla.Columnas[0];
        var columnaDiferida = tabla.Columnas.Single(c => c.Nombre == tabla.Diferida);
        await using var comando = new MySqlCommand(
            $"UPDATE `{tabla.Nombre}` SET `{columnaDiferida.Nombre}` = @valor WHERE `{columnaId.Nombre}` = @id", conexion, transaccion)
        {
            CommandTimeout = ConexionRespaldo.SegundosOperacion
        };
        comando.Parameters.Add(Parametro("@valor", columnaDiferida.Tipo, valor));
        comando.Parameters.Add(Parametro("@id", columnaId.Tipo, id));
        if (await comando.ExecuteNonQueryAsync(ct) != 1)
            throw ErroresRespaldo.CopiaDanada($"{tabla.Nombre}: no se pudo completar una referencia diferida.");
    }

    private static MySqlParameter Parametro(string nombre, TipoColumna tipo, object? valor) => new(nombre, tipo switch
    {
        TipoColumna.Id => MySqlDbType.Guid,
        TipoColumna.Texto => MySqlDbType.VarChar,
        TipoColumna.Fecha => MySqlDbType.Date,
        TipoColumna.Entero => MySqlDbType.Int64,
        TipoColumna.Decimal => MySqlDbType.NewDecimal,
        _ => throw new InvalidOperationException("Tipo de columna no previsto.")
    }) { Value = valor ?? DBNull.Value };
}
