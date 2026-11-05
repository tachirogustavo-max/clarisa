using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Esquema;

// Compara el catálogo real de la base con EsquemaEsperado hasta una versión. Devuelve discrepancias legibles, nunca datos de filas.
internal static class VerificadorEsquema
{
    // Con soloExistentes se ignoran las tablas todavía no creadas: sirve para saber si un DDL falló por una tabla previa distinta.
    public static async Task<IReadOnlyList<string>> VerificarAsync(MySqlConnection conexion, int hastaVersion, int segundosComando,
        CancellationToken ct, bool soloExistentes = false)
    {
        var esperadas = EsquemaEsperado.Tablas.Where(tabla => tabla.VigenteEn(hastaVersion)).ToList();
        var conocidas = EsquemaEsperado.Tablas.Select(tabla => tabla.Nombre).ToHashSet(StringComparer.Ordinal);
        var discrepancias = new List<string>();

        var tablas = await LeerAsync(conexion, segundosComando, ct,
            "SELECT TABLE_NAME, IFNULL(ENGINE, ''), TABLE_TYPE FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE()",
            fila => (Nombre: fila.GetString(0), Motor: fila.GetString(1), Tipo: fila.GetString(2)));
        if (soloExistentes) esperadas = esperadas.Where(tabla => tablas.Any(t => t.Nombre == tabla.Nombre)).ToList();
        foreach (var ajena in tablas.Where(tabla => !conocidas.Contains(tabla.Nombre)))
            discrepancias.Add($"Objeto no previsto: {ajena.Nombre} ({ajena.Tipo}).");
        foreach (var tabla in esperadas)
        {
            var real = tablas.FirstOrDefault(t => t.Nombre == tabla.Nombre);
            if (real.Nombre is null) discrepancias.Add($"Falta la tabla {tabla.Nombre}.");
            else if (real.Motor != "InnoDB" || real.Tipo != "BASE TABLE") discrepancias.Add($"La tabla {tabla.Nombre} no es InnoDB.");
        }

        var columnas = await AgruparAsync(conexion, segundosComando, ct, """
            SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE, IS_NULLABLE,
                COLLATION_NAME, IS_GENERATED
            FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
            """, fila => (fila.GetString(0), DescribirColumna(fila)));
        var restricciones = await AgruparAsync(conexion, segundosComando, ct,
            "SELECT TABLE_NAME, CONSTRAINT_NAME FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE()",
            fila => (fila.GetString(0), fila.GetString(1)));
        var indices = await AgruparAsync(conexion, segundosComando, ct,
            "SELECT DISTINCT TABLE_NAME, INDEX_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE()",
            fila => (fila.GetString(0), fila.GetString(1)));

        foreach (var tabla in esperadas)
        {
            Comparar(discrepancias, tabla.Nombre, "columnas", EsquemaEsperado.ColumnasNormalizadas(tabla), columnas, exacto: true);
            Comparar(discrepancias, tabla.Nombre, "restricciones", Separar(tabla.Restricciones), restricciones, exacto: true);
            // InnoDB puede crear índices implícitos para claves foráneas: solo se exige que existan los declarados.
            Comparar(discrepancias, tabla.Nombre, "índices", Separar(tabla.Indices), indices, exacto: false);
        }

        if (soloExistentes) return discrepancias;
        await VerificarDisparadoresAsync(conexion, hastaVersion, segundosComando, discrepancias, ct);
        if (hastaVersion >= 1 && !discrepancias.Any(d => d.Contains("bloqueo_escritura", StringComparison.Ordinal)))
        {
            var filas = await LeerAsync(conexion, segundosComando, ct, "SELECT COUNT(*), IFNULL(SUM(id = 1), 0) FROM bloqueo_escritura",
                fila => (Total: fila.GetInt64(0), Turno: Convert.ToInt64(fila.GetValue(1), System.Globalization.CultureInfo.InvariantCulture)));
            if (filas[0] != (1, 1)) discrepancias.Add("La tabla bloqueo_escritura debe tener exactamente la fila 1.");
        }
        return discrepancias;
    }

    private static async Task VerificarDisparadoresAsync(MySqlConnection conexion, int hastaVersion, int segundosComando,
        List<string> discrepancias, CancellationToken ct)
    {
        var reales = (await LeerAsync(conexion, segundosComando, ct, """
            SELECT TRIGGER_NAME, EVENT_OBJECT_TABLE, ACTION_TIMING, EVENT_MANIPULATION
            FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA = DATABASE()
            """, fila => $"{fila.GetString(0)} {fila.GetString(1)} {fila.GetString(2)} {fila.GetString(3)}")).ToHashSet();
        var esperados = EsquemaEsperado.Disparadores.Where(d => d.Version <= hastaVersion)
            .Select(d => $"{d.Nombre} {d.Tabla} {d.Momento} {d.Evento}").ToHashSet();
        var todos = EsquemaEsperado.Disparadores.Select(d => $"{d.Nombre} {d.Tabla} {d.Momento} {d.Evento}").ToHashSet();
        discrepancias.AddRange(esperados.Except(reales).Select(d => $"Falta o cambió el disparador {d}."));
        discrepancias.AddRange(reales.Except(todos).Select(d => $"Disparador no previsto: {d}."));
    }

    private static void Comparar(List<string> discrepancias, string tabla, string tipo, IEnumerable<string> esperados,
        IReadOnlyDictionary<string, HashSet<string>> reales, bool exacto)
    {
        var real = reales.GetValueOrDefault(tabla) ?? [];
        var esperado = esperados.ToHashSet(StringComparer.Ordinal);
        foreach (var falta in esperado.Except(real)) discrepancias.Add($"{tabla}: falta o difiere en {tipo}: {falta}.");
        if (!exacto) return;
        foreach (var sobra in real.Except(esperado)) discrepancias.Add($"{tabla}: {tipo} no previstas: {sobra}.");
    }

    private static string DescribirColumna(MySqlDataReader fila)
    {
        var tipo = fila.GetString(2);
        var descripcion = tipo switch
        {
            "char" or "varchar" => $"{tipo}({fila.GetInt64(3)}) {(fila.IsDBNull(7) ? "" : fila.GetString(7))}",
            "decimal" => $"decimal({fila.GetInt64(4)},{fila.GetInt64(5)})",
            _ => tipo
        };
        var nulo = fila.GetString(6) == "YES" ? "?" : "";
        var generada = fila.GetString(8) == "ALWAYS" ? "*" : "";
        return $"{fila.GetString(1)}{nulo}:{descripcion}{generada}";
    }

    private static IEnumerable<string> Separar(string nombres) => nombres.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static async Task<IReadOnlyDictionary<string, HashSet<string>>> AgruparAsync(MySqlConnection conexion, int segundosComando,
        CancellationToken ct, string sql, Func<MySqlDataReader, (string Tabla, string Valor)> convertir) =>
        (await LeerAsync(conexion, segundosComando, ct, sql, convertir))
            .GroupBy(par => par.Tabla, StringComparer.Ordinal)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Select(par => par.Valor).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

    internal static async Task<List<T>> LeerAsync<T>(MySqlConnection conexion, int segundosComando, CancellationToken ct, string sql,
        Func<MySqlDataReader, T> convertir)
    {
        await using var comando = new MySqlCommand(sql, conexion) { CommandTimeout = segundosComando };
        await using var lector = await comando.ExecuteReaderAsync(ct);
        var filas = new List<T>();
        while (await lector.ReadAsync(ct)) filas.Add(convertir(lector));
        return filas;
    }
}
