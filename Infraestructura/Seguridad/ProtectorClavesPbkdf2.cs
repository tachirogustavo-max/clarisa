using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Application.Common.Errores;
using Clarisa.Infrastructure.Persistence.Sesion;

namespace Clarisa.Infrastructure.Seguridad;

// PBKDF2-HMAC-SHA256 de la BCL. Formato: pbkdf2-sha256$v1$<iteraciones>$<sal base64>$<hash base64>. Las iteraciones guardadas
// permiten verificar claves antiguas y pedir que se rehagan cuando se sube el costo configurado.
public sealed class ProtectorClavesPbkdf2 : IProtectorClaves
{
    public const int IteracionesMinimas = 100_000;
    public const int IteracionesMaximas = 5_000_000;
    public const int IteracionesPredeterminadas = 600_000;

    private const string Algoritmo = "pbkdf2-sha256";
    private const string Version = "v1";
    private const int BytesSal = 16;
    private const int BytesHash = 32;

    private static readonly UTF8Encoding Utf8Estricto = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly int iteraciones;

    public ProtectorClavesPbkdf2(int iteraciones = IteracionesPredeterminadas)
    {
        if (iteraciones is < IteracionesMinimas or > IteracionesMaximas)
            throw new ArgumentOutOfRangeException(nameof(iteraciones), $"Usa de {IteracionesMinimas} a {IteracionesMaximas} iteraciones.");
        this.iteraciones = iteraciones;
    }

    public string Calcular(string clave)
    {
        ArgumentNullException.ThrowIfNull(clave);
        var sal = RandomNumberGenerator.GetBytes(BytesSal);
        var hash = Derivar(clave, sal, iteraciones);
        return string.Join('$', Algoritmo, Version, iteraciones.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(sal), Convert.ToBase64String(hash));
    }

    public VerificacionClave Verificar(string clave, string huellaAlmacenada)
    {
        ArgumentNullException.ThrowIfNull(clave);
        var (iteracionesGuardadas, sal, esperado) = Leer(huellaAlmacenada);
        var obtenido = Derivar(clave, sal, iteracionesGuardadas);
        if (!CryptographicOperations.FixedTimeEquals(obtenido, esperado)) return VerificacionClave.Incorrecta;
        return iteracionesGuardadas < iteraciones ? VerificacionClave.CorrectaRequiereActualizar : VerificacionClave.Correcta;
    }

    private static byte[] Derivar(string clave, byte[] sal, int iteraciones)
    {
        var bytes = Utf8Estricto.GetBytes(clave);
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(bytes, sal, iteraciones, HashAlgorithmName.SHA256, BytesHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    // Una huella que no respeta el formato es un dato dañado: se informa de forma tipada en lugar de tratarla como clave incorrecta.
    private static (int Iteraciones, byte[] Sal, byte[] Hash) Leer(string? huella)
    {
        var partes = (huella ?? "").Split('$');
        if (partes.Length == 5 && partes[0] == Algoritmo && partes[1] == Version
            && int.TryParse(partes[2], NumberStyles.None, CultureInfo.InvariantCulture, out var vueltas)
            && vueltas is >= 1_000 and <= IteracionesMaximas
            && Decodificar(partes[3]) is { Length: BytesSal } sal && Decodificar(partes[4]) is { Length: BytesHash } hash)
            return (vueltas, sal, hash);
        throw Error.AlmacenNoDisponible(new DatoAlmacenadoInvalidoException("La huella de clave almacenada no tiene un formato válido."));
    }

    private static byte[]? Decodificar(string texto)
    {
        var destino = new byte[(texto.Length * 3 + 3) / 4];
        return Convert.TryFromBase64String(texto, destino, out var escritos) ? destino[..escritos] : null;
    }
}
