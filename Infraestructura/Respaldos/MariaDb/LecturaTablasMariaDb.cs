using System.Buffers;
using System.Security.Cryptography;
using Clarisa.Infrastructure.Persistence.Esquema;
using Clarisa.Infrastructure.Respaldos.Formato;
using MySqlConnector;

namespace Clarisa.Infrastructure.Respaldos.MariaDb;

// Lectura canónica compartida por la copia y por la verificación de la base recuperada: mismos nombres, mismo orden y mismos bytes.
internal static class LecturaTablasMariaDb
{
    public static async Task<long> RecorrerAsync(MySqlConnection conexion, TablaRespaldo tabla, Action<ReadOnlyMemory<byte>> alLinea,
        CancellationToken ct)
    {
        var columnas = string.Join(", ", tabla.Columnas.Select(c => $"`{c.Nombre}`"));
        await using var comando = new MySqlCommand($"SELECT {columnas} FROM `{tabla.Nombre}` ORDER BY `{tabla.ClaveOrden}`", conexion)
        {
            CommandTimeout = ConexionRespaldo.SegundosOperacion
        };
        await using var lector = await comando.ExecuteReaderAsync(ct);
        var bufer = new ArrayBufferWriter<byte>(512);
        long filas = 0;
        while (await lector.ReadAsync(ct))
        {
            CodificacionFilas.Escribir(lector, tabla, bufer);
            alLinea(bufer.WrittenMemory);
            filas++;
        }
        return filas;
    }

    public static async Task<string> HuellaContenidoAsync(MySqlConnection conexion, CancellationToken ct)
    {
        var tablas = new List<(string, long, string)>();
        foreach (var tabla in CatalogoRespaldo.Tablas)
        {
            using var huella = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var filas = await RecorrerAsync(conexion, tabla, linea =>
            {
                huella.AppendData(linea.Span);
                huella.AppendData("\n"u8);
            }, ct);
            tablas.Add((tabla.Nombre, filas, Convert.ToHexString(huella.GetHashAndReset())));
        }
        return Manifiesto.HuellaContenido(tablas);
    }

    // La base debe tener exactamente las migraciones de esta aplicación y las tablas/columnas del catálogo (más las excluidas).
    public static async Task<IReadOnlyList<MigracionCopiada>> ExigirEsquemaAsync(MySqlConnection conexion, CancellationToken ct)
    {
        var migraciones = new List<MigracionCopiada>();
        await using (var comando = new MySqlCommand(
                         $"SELECT version, huella FROM `{EsquemaEsperado.TablaVersiones}` ORDER BY version", conexion))
        await using (var lector = await comando.ExecuteReaderAsync(ct))
        {
            while (await lector.ReadAsync(ct)) migraciones.Add(new(lector.GetInt32(0), lector.GetString(1)));
        }
        if (!MigracionesActuales().SequenceEqual(migraciones)) throw ErroresRespaldo.Incompatible();

        var reales = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        await using (var comando = new MySqlCommand("""
                         SELECT TABLE_NAME, COLUMN_NAME, IS_GENERATED FROM information_schema.COLUMNS
                         WHERE TABLE_SCHEMA = DATABASE() ORDER BY TABLE_NAME, ORDINAL_POSITION
                         """, conexion))
        await using (var lector = await comando.ExecuteReaderAsync(ct))
        {
            while (await lector.ReadAsync(ct))
            {
                var (tablaReal, columna, generada) = (lector.GetString(0), lector.GetString(1), lector.GetString(2) == "ALWAYS");
                var esperada = CatalogoRespaldo.Generadas.GetValueOrDefault(tablaReal) == columna;
                if (generada != esperada) throw ErroresRespaldo.EsquemaNoReconocido($"columna generada inesperada en {tablaReal}.");
                if (!generada) (reales.TryGetValue(tablaReal, out var lista) ? lista : reales[tablaReal] = []).Add(columna);
            }
        }
        foreach (var tabla in CatalogoRespaldo.Tablas)
        {
            if (!reales.Remove(tabla.Nombre, out var columnas) || !columnas.SequenceEqual(tabla.Columnas.Select(c => c.Nombre)))
                throw ErroresRespaldo.EsquemaNoReconocido($"la tabla {tabla.Nombre} no coincide.");
        }
        var ajenas = reales.Keys.Where(t => !CatalogoRespaldo.Excluidas.Contains(t)).ToList();
        if (ajenas.Count > 0) throw ErroresRespaldo.EsquemaNoReconocido("tablas no previstas: " + string.Join(", ", ajenas) + ".");
        return migraciones;
    }

    public static IReadOnlyList<MigracionCopiada> MigracionesActuales() =>
        CatalogoMigraciones.Disponibles.Select(m => new MigracionCopiada(m.Version, m.Huella)).ToList();
}
