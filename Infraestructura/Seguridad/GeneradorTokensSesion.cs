using System.Buffers.Text;
using System.Security.Cryptography;
using Clarisa.Application.Abstractions.Seguridad;

namespace Clarisa.Infrastructure.Seguridad;

// 256 bits de RandomNumberGenerator en Base64Url. En la base solo se guarda el SHA-256: quien lea la tabla no obtiene cookies válidas.
public sealed class GeneradorTokensSesion : IGeneradorTokensSesion
{
    private const int Bytes = 32;
    private const int LongitudTexto = 43;

    public TokenSesion Generar()
    {
        var bytes = RandomNumberGenerator.GetBytes(Bytes);
        try
        {
            return new TokenSesion(Base64Url.EncodeToString(bytes), Convert.ToHexString(SHA256.HashData(bytes)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    // Solo la codificación canónica: un carácter final con bits sobrantes distintos no debe abrir la misma sesión.
    public string? Huella(string? valor)
    {
        if (valor is not { Length: LongitudTexto }) return null;
        Span<byte> bytes = stackalloc byte[Bytes];
        try
        {
            if (!Base64Url.TryDecodeFromChars(valor, bytes, out var escritos) || escritos != Bytes) return null;
            if (!string.Equals(Base64Url.EncodeToString(bytes), valor, StringComparison.Ordinal)) return null;
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        catch (FormatException) { return null; }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
