using Clarisa.Application.Features.Respaldos;
using Clarisa.Infrastructure.Configuration;
using Clarisa.Infrastructure.Persistence;
using MySqlConnector;

namespace Clarisa.Infrastructure.Respaldos.MariaDb;

// Conexiones sin pool: una transacción de snapshot o de carga nunca vuelve a un pool compartido con la API.
internal static class ConexionRespaldo
{
    public const int SegundosOperacion = 600;

    public static ConfiguracionMariaDb ComoOperador(ConfiguracionMariaDb aplicacion, string baseDatos, CredencialOperador operador) =>
        aplicacion with
        {
            Base = baseDatos, Usuario = operador.Usuario, Clave = operador.Clave, SegundosComando = 300,
            SegundosEsperaBloqueo = Math.Min(aplicacion.SegundosEsperaBloqueo, 299), MaximoConexiones = 2
        };

    public static async Task<MySqlConnection> AbrirAsync(ConfiguracionMariaDb configuracion, bool sinBase, CancellationToken ct)
    {
        configuracion.Validar();
        var cadena = new MySqlConnectionStringBuilder
        {
            Server = configuracion.Servidor, Port = (uint)configuracion.Puerto, Database = sinBase ? "" : configuracion.Base,
            UserID = configuracion.Usuario, Password = configuracion.Clave, Pooling = false,
            ConnectionTimeout = (uint)configuracion.SegundosConexion, DefaultCommandTimeout = SegundosOperacion,
            AllowUserVariables = false, AllowLoadLocalInfile = false, AllowPublicKeyRetrieval = false, UseAffectedRows = false,
            GuidFormat = MySqlGuidFormat.Char36, CharacterSet = "utf8mb4", ApplicationName = "Clarisa.Respaldos"
        }.ConnectionString;
        var conexion = new MySqlConnection(cadena);
        try
        {
            await conexion.OpenAsync(ct);
            await EjecutarAsync(conexion, "SET SESSION sql_mode = @modo, SESSION time_zone = '+00:00'", ct,
                ("@modo", FabricaUnidadTrabajoMariaDb.ModoSql));
            return conexion;
        }
        catch
        {
            await conexion.DisposeAsync();
            throw;
        }
    }

    public static async Task<long> EjecutarAsync(MySqlConnection conexion, string sql, CancellationToken ct,
        params (string Nombre, object? Valor)[] parametros)
    {
        await using var comando = new MySqlCommand(sql, conexion) { CommandTimeout = SegundosOperacion };
        foreach (var (nombre, valor) in parametros) comando.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);
        return await comando.ExecuteNonQueryAsync(ct);
    }

    public static async Task<long> EscalarAsync(MySqlConnection conexion, string sql, CancellationToken ct,
        params (string Nombre, object? Valor)[] parametros)
    {
        await using var comando = new MySqlCommand(sql, conexion) { CommandTimeout = SegundosOperacion };
        foreach (var (nombre, valor) in parametros) comando.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);
        var resultado = await comando.ExecuteScalarAsync(ct);
        return resultado is null or DBNull ? 0 : Convert.ToInt64(resultado, System.Globalization.CultureInfo.InvariantCulture);
    }
}
