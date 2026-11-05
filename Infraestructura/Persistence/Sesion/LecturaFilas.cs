using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Sesion;

internal sealed class DatoAlmacenadoInvalidoException(string mensaje) : Exception(mensaje);

internal static class LecturaFilas
{
    public static Guid Id(this MySqlDataReader fila, int indice) => fila.GetGuid(indice);

    public static Guid? IdOpcional(this MySqlDataReader fila, int indice) => fila.IsDBNull(indice) ? null : fila.GetGuid(indice);

    public static string? TextoOpcional(this MySqlDataReader fila, int indice) => fila.IsDBNull(indice) ? null : fila.GetString(indice);

    public static DateOnly Fecha(this MySqlDataReader fila, int indice) => fila.GetFieldValue<DateOnly>(indice);

    public static DateOnly? FechaOpcional(this MySqlDataReader fila, int indice) =>
        fila.IsDBNull(indice) ? null : fila.GetFieldValue<DateOnly>(indice);

    public static bool Logico(this MySqlDataReader fila, int indice) => fila.GetSByte(indice) switch
    {
        0 => false,
        1 => true,
        var otro => throw new DatoAlmacenadoInvalidoException($"Valor lógico almacenado no válido: {otro}.")
    };

    public static decimal? Nota(this MySqlDataReader fila, int indice) => fila.IsDBNull(indice) ? null : fila.GetDecimal(indice);

    public static DateTimeOffset Instante(this MySqlDataReader fila, int indice) => DesdeTicks(fila.GetInt64(indice));

    public static DateTimeOffset? InstanteOpcional(this MySqlDataReader fila, int indice) =>
        fila.IsDBNull(indice) ? null : DesdeTicks(fila.GetInt64(indice));

    public static T Enumerado<T>(this MySqlDataReader fila, int indice) where T : struct, Enum
    {
        var valor = fila.GetInt32(indice);
        var convertido = (T)Enum.ToObject(typeof(T), valor);
        return Enum.IsDefined(convertido)
            ? convertido
            : throw new DatoAlmacenadoInvalidoException($"Valor almacenado no válido para {typeof(T).Name}: {valor}.");
    }

    private static DateTimeOffset DesdeTicks(long ticks) =>
        ticks is >= 0 and <= 3_155_378_975_999_999_999
            ? new DateTimeOffset(ticks, TimeSpan.Zero)
            : throw new DatoAlmacenadoInvalidoException("Instante almacenado fuera de rango.");
}
