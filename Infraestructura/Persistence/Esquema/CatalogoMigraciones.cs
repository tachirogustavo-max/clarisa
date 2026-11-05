using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Clarisa.Infrastructure.Persistence.Esquema;

internal sealed record Migracion(int Version, string Nombre, IReadOnlyList<string> Sentencias, string Huella);

// Recursos embebidos «Clarisa.Migraciones.NNNN_nombre.sql». Una migración aplicada no se edita: los cambios van en un archivo nuevo.
internal static partial class CatalogoMigraciones
{
    private const string Prefijo = "Clarisa.Migraciones.";

    private static readonly Lazy<IReadOnlyList<Migracion>> disponibles = new(Cargar);

    public static IReadOnlyList<Migracion> Disponibles => disponibles.Value;

    public static int VersionMaxima => Disponibles[^1].Version;

    // Cada sentencia termina con «;» al final de una línea; las líneas «--» se ignoran. No se admiten DELIMITER ni varias por línea.
    internal static IReadOnlyList<string> Separar(string sql, string nombre)
    {
        var sentencias = new List<string>();
        var actual = new StringBuilder();
        foreach (var linea in sql.Split('\n'))
        {
            var limpia = linea.TrimEnd();
            if (limpia.TrimStart().StartsWith("--", StringComparison.Ordinal) || limpia.Length == 0) continue;
            if (limpia.Contains("DELIMITER", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"La migración {nombre} usa DELIMITER, que el migrador no admite.");
            actual.Append(limpia).Append('\n');
            if (!limpia.EndsWith(';')) continue;
            var sentencia = actual.ToString().Trim().TrimEnd(';').Trim();
            if (sentencia.Contains(';'))
                throw new InvalidOperationException($"La migración {nombre} tiene varias sentencias en una línea.");
            sentencias.Add(sentencia);
            actual.Clear();
        }
        if (actual.ToString().Trim().Length > 0)
            throw new InvalidOperationException($"La migración {nombre} termina con una sentencia sin «;».");
        return sentencias;
    }

    internal static string Normalizar(string sql) => sql.TrimStart('﻿').Replace("\r\n", "\n", StringComparison.Ordinal);

    internal static string Huella(string sqlNormalizado) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sqlNormalizado)));

    private static IReadOnlyList<Migracion> Cargar()
    {
        var ensamblado = typeof(CatalogoMigraciones).Assembly;
        var migraciones = new List<Migracion>();
        foreach (var recurso in ensamblado.GetManifestResourceNames().Where(n => n.StartsWith(Prefijo, StringComparison.Ordinal)))
        {
            var nombre = recurso[Prefijo.Length..^".sql".Length];
            var coincidencia = NombreMigracion().Match(nombre);
            if (!coincidencia.Success) throw new InvalidOperationException($"Nombre de migración no válido: {nombre}.");
            using var flujo = ensamblado.GetManifestResourceStream(recurso)
                ?? throw new InvalidOperationException($"No se pudo leer la migración {nombre}.");
            using var lector = new StreamReader(flujo, Encoding.UTF8);
            var sql = Normalizar(lector.ReadToEnd());
            migraciones.Add(new Migracion(int.Parse(coincidencia.Groups["version"].Value, System.Globalization.CultureInfo.InvariantCulture),
                nombre, Separar(sql, nombre), Huella(sql)));
        }
        var ordenadas = migraciones.OrderBy(m => m.Version).ToList();
        if (ordenadas.Count == 0 || ordenadas.Where((m, indice) => m.Version != indice + 1).Any())
            throw new InvalidOperationException("Las migraciones deben numerarse de forma consecutiva desde 0001.");
        return ordenadas;
    }

    [GeneratedRegex("^(?<version>[0-9]{4})_[a-z0-9_]{1,100}$")]
    private static partial Regex NombreMigracion();
}
