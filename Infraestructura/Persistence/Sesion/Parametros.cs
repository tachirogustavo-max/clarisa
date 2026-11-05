using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Sesion;

internal readonly record struct Parametro(string Nombre, MySqlDbType Tipo, object? Valor);

internal static class Parametros
{
    public static Parametro Id(string nombre, Guid valor) => new(nombre, MySqlDbType.Guid, valor);

    public static Parametro IdOpcional(string nombre, Guid? valor) => new(nombre, MySqlDbType.Guid, valor);

    public static Parametro Texto(string nombre, string? valor) => new(nombre, MySqlDbType.VarChar, valor);

    public static Parametro Fecha(string nombre, DateOnly valor) => new(nombre, MySqlDbType.Date, valor);

    public static Parametro FechaOpcional(string nombre, DateOnly? valor) => new(nombre, MySqlDbType.Date, valor);

    public static Parametro Entero(string nombre, int valor) => new(nombre, MySqlDbType.Int32, valor);

    public static Parametro EnteroOpcional(string nombre, int? valor) => new(nombre, MySqlDbType.Int32, valor);

    public static Parametro Largo(string nombre, long valor) => new(nombre, MySqlDbType.Int64, valor);

    public static Parametro Logico(string nombre, bool valor) => new(nombre, MySqlDbType.Byte, valor ? (sbyte)1 : (sbyte)0);

    public static Parametro Nota(string nombre, decimal? valor) => new(nombre, MySqlDbType.NewDecimal, valor);

    // Los instantes se guardan como ticks UTC: exactos a 100 ns e independientes de la zona del servidor.
    public static Parametro Instante(string nombre, DateTimeOffset valor) => new(nombre, MySqlDbType.Int64, valor.UtcTicks);

    public static Parametro InstanteOpcional(string nombre, DateTimeOffset? valor) => new(nombre, MySqlDbType.Int64, valor?.UtcTicks);

    public static Parametro Patron(string nombre, string texto) => Texto(nombre, "%" + EscaparComodines(texto) + "%");

    // Se usa con ESCAPE '!' para que %, _ y ! escritos por el usuario se busquen literalmente.
    private static string EscaparComodines(string texto) =>
        texto.Replace("!", "!!", StringComparison.Ordinal).Replace("%", "!%", StringComparison.Ordinal)
            .Replace("_", "!_", StringComparison.Ordinal);
}
