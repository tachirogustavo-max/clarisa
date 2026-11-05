using System.Security.Cryptography;
using System.Text.Json;

namespace Clarisa.Api.Security;

public sealed record EntradaCuentaInicial(string Usuario, string Nombre, string Clave)
{
    public override string ToString() => "Datos de la primera cuenta (protegidos)";
}

// Protocolo del instalador: un único objeto JSON con exactamente usuario, nombre y clave, leído de la entrada estándar con tamaño acotado.
// Cualquier desviación (propiedad repetida o desconocida, tipo distinto, contenido adicional) invalida la entrada completa.
public static class LectorEntradaCuentaInicial
{
    public const int MaximoBytes = 4096;

    public static async Task<EntradaCuentaInicial?> LeerAsync(Stream entrada, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        var bufer = new byte[MaximoBytes + 1];
        var total = 0;
        try
        {
            while (total < bufer.Length)
            {
                var leidos = await entrada.ReadAsync(bufer.AsMemory(total), ct);
                if (leidos == 0) break;
                total += leidos;
            }
            return total > MaximoBytes ? null : Interpretar(bufer.AsSpan(0, total));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bufer);
        }
    }

    public static EntradaCuentaInicial? Interpretar(ReadOnlySpan<byte> datos)
    {
        if (datos.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF])) datos = datos[3..];
        var lector = new Utf8JsonReader(datos, new JsonReaderOptions { MaxDepth = 1 });
        string? usuario = null, nombre = null, clave = null;
        try
        {
            if (!lector.Read() || lector.TokenType != JsonTokenType.StartObject) return null;
            while (lector.Read() && lector.TokenType == JsonTokenType.PropertyName)
            {
                var propiedad = lector.GetString();
                if (!lector.Read() || lector.TokenType != JsonTokenType.String) return null;
                switch (propiedad)
                {
                    case "usuario" when usuario is null:
                        usuario = lector.GetString();
                        break;
                    case "nombre" when nombre is null:
                        nombre = lector.GetString();
                        break;
                    case "clave" when clave is null:
                        clave = lector.GetString();
                        break;
                    default:
                        return null;
                }
            }
            if (lector.TokenType != JsonTokenType.EndObject || lector.Read()) return null;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            return null;
        }
        return usuario is null || nombre is null || clave is null ? null : new(usuario, nombre, clave);
    }
}
