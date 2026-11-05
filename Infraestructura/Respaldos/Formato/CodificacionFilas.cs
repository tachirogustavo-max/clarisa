using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Clarisa.Application.Common.Errores;
using MySqlConnector;

namespace Clarisa.Infrastructure.Respaldos.Formato;

// Una fila = arreglo JSON en el orden del catálogo. Representación canónica: la misma fila siempre produce los mismos bytes, de modo que
// la huella de una tabla recuperada puede compararse con la del manifiesto.
internal static class CodificacionFilas
{
    public const int MaximoBytesLinea = 64 * 1024;
    private const int MaximoTexto = 4000;

    public static void Escribir(MySqlDataReader fila, TablaRespaldo tabla, ArrayBufferWriter<byte> destino)
    {
        destino.ResetWrittenCount();
        using var escritor = new Utf8JsonWriter(destino, new JsonWriterOptions { SkipValidation = false });
        escritor.WriteStartArray();
        for (var indice = 0; indice < tabla.Columnas.Count; indice++)
        {
            if (fila.IsDBNull(indice))
            {
                escritor.WriteNullValue();
                continue;
            }
            switch (tabla.Columnas[indice].Tipo)
            {
                case TipoColumna.Id: escritor.WriteStringValue(fila.GetGuid(indice).ToString("D")); break;
                case TipoColumna.Texto: escritor.WriteStringValue(fila.GetString(indice)); break;
                case TipoColumna.Fecha: escritor.WriteStringValue(fila.GetFieldValue<DateOnly>(indice).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); break;
                case TipoColumna.Entero: escritor.WriteNumberValue(Convert.ToInt64(fila.GetValue(indice), CultureInfo.InvariantCulture)); break;
                case TipoColumna.Decimal: escritor.WriteStringValue(fila.GetDecimal(indice).ToString(CultureInfo.InvariantCulture)); break;
                default: throw new InvalidOperationException("Tipo de columna no previsto.");
            }
        }
        escritor.WriteEndArray();
        escritor.Flush();
        if (destino.WrittenCount > MaximoBytesLinea) throw ErroresRespaldo.Excede("64 KiB por registro");
    }

    // Valores: Guid, string, DateOnly, long, decimal o null. Rechaza cualquier forma no canónica en lugar de normalizarla.
    public static object?[] Leer(ReadOnlySpan<byte> linea, TablaRespaldo tabla)
    {
        try
        {
            return Interpretar(linea, tabla);
        }
        catch (JsonException error)
        {
            throw ErroresRespaldo.CopiaDanada($"{tabla.Nombre}: registro con JSON no válido.", error);
        }
    }

    private static object?[] Interpretar(ReadOnlySpan<byte> linea, TablaRespaldo tabla)
    {
        var lector = new Utf8JsonReader(linea, new JsonReaderOptions { MaxDepth = 2 });
        if (!lector.Read() || lector.TokenType != JsonTokenType.StartArray) throw Danada(tabla, "no es un arreglo");
        var valores = new object?[tabla.Columnas.Count];
        for (var indice = 0; indice < valores.Length; indice++)
        {
            if (!lector.Read()) throw Danada(tabla, "registro incompleto");
            valores[indice] = Valor(ref lector, tabla, tabla.Columnas[indice]);
        }
        if (!lector.Read() || lector.TokenType != JsonTokenType.EndArray || lector.Read()) throw Danada(tabla, "columnas sobrantes");
        return valores;
    }

    private static object? Valor(ref Utf8JsonReader lector, TablaRespaldo tabla, ColumnaRespaldo columna)
    {
        if (lector.TokenType == JsonTokenType.Null)
            return columna.Nula ? null : throw Danada(tabla, $"valor vacío en {columna.Nombre}");
        if (columna.Tipo == TipoColumna.Entero)
            return lector.TokenType == JsonTokenType.Number && lector.TryGetInt64(out var entero) && Canonico(ref lector, entero)
                ? entero : throw Danada(tabla, $"entero no válido en {columna.Nombre}");
        if (lector.TokenType != JsonTokenType.String) throw Danada(tabla, $"tipo no válido en {columna.Nombre}");
        var texto = lector.GetString()!;
        return columna.Tipo switch
        {
            TipoColumna.Id when Guid.TryParseExact(texto, "D", out var id) && id.ToString("D") == texto => id,
            TipoColumna.Texto when texto.Length <= MaximoTexto => texto,
            TipoColumna.Fecha when DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha) => fecha,
            TipoColumna.Decimal when decimal.TryParse(texto, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var numero) && numero.ToString(CultureInfo.InvariantCulture) == texto => numero,
            _ => throw Danada(tabla, $"valor no válido en {columna.Nombre}")
        };
    }

    private static bool Canonico(ref Utf8JsonReader lector, long entero)
    {
        Span<byte> esperado = stackalloc byte[24];
        return entero.TryFormat(esperado, out var escritos, default, CultureInfo.InvariantCulture)
            && lector.ValueSpan.SequenceEqual(esperado[..escritos]);
    }

    private static ErrorAplicacionException Danada(TablaRespaldo tabla, string detalle) =>
        ErroresRespaldo.CopiaDanada($"{tabla.Nombre}: {detalle}.");
}
