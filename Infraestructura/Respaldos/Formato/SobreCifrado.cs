using System.Buffers.Binary;
using System.Security.Cryptography;
using Clarisa.Infrastructure.Respaldos.Claves;

namespace Clarisa.Infrastructure.Respaldos.Formato;

// Encabezado autenticado como datos asociados + etiqueta GCM + texto cifrado. AES-256-GCM de .NET con nonce aleatorio por copia.
internal static class SobreCifrado
{
    public const int LongitudEncabezado = 48;
    public const int LongitudEtiqueta = 16;
    public const int LongitudNonce = 12;
    public const int Inicio = LongitudEncabezado + LongitudEtiqueta;
    private const ushort VersionSobre = 1;
    private static readonly byte[] Magia = "LCETRSP1"u8.ToArray();

    public static void Escribir(Stream destino, ClaveRespaldo clave, ReadOnlySpan<byte> plano)
    {
        var encabezado = new byte[LongitudEncabezado];
        Magia.CopyTo(encabezado, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(encabezado.AsSpan(8), VersionSobre);
        clave.Id.TryWriteBytes(encabezado.AsSpan(12, 16));
        RandomNumberGenerator.Fill(encabezado.AsSpan(28, LongitudNonce));
        BinaryPrimitives.WriteInt64LittleEndian(encabezado.AsSpan(40), plano.Length);
        var etiqueta = new byte[LongitudEtiqueta];
        var cifrado = new byte[plano.Length];
        using (var aes = new AesGcm(clave.Material, LongitudEtiqueta))
            aes.Encrypt(encabezado.AsSpan(28, LongitudNonce), plano, cifrado, etiqueta, encabezado);
        destino.Write(encabezado);
        destino.Write(etiqueta);
        destino.Write(cifrado);
    }

    public static Guid LeerIdClave(Stream origen)
    {
        var encabezado = LeerEncabezado(origen, out _, long.MaxValue);
        return new Guid(encabezado.AsSpan(12, 16));
    }

    // Exige que el archivo tenga exactamente la longitud declarada: un truncado o un añadido se rechazan antes de descifrar.
    public static byte[] Leer(Stream origen, ClaveRespaldo clave, long maximoCifrado)
    {
        var encabezado = LeerEncabezado(origen, out var longitud, maximoCifrado);
        if (origen.CanSeek && origen.Length - origen.Position != LongitudEtiqueta + longitud)
            throw ErroresRespaldo.CopiaDanada("la longitud del archivo no coincide con la declarada.");
        var etiqueta = new byte[LongitudEtiqueta];
        var cifrado = new byte[longitud];
        LeerExacto(origen, etiqueta);
        LeerExacto(origen, cifrado);
        if (origen.ReadByte() != -1) throw ErroresRespaldo.CopiaDanada("hay datos después del contenido cifrado.");

        var idClave = new Guid(encabezado.AsSpan(12, 16));
        if (idClave != clave.Id) throw ErroresRespaldo.OtraClave(idClave);
        var plano = new byte[longitud];
        try
        {
            using var aes = new AesGcm(clave.Material, LongitudEtiqueta);
            aes.Decrypt(encabezado.AsSpan(28, LongitudNonce), cifrado, etiqueta, plano, encabezado);
            return plano;
        }
        catch (AuthenticationTagMismatchException error)
        {
            CryptographicOperations.ZeroMemory(plano);
            throw ErroresRespaldo.ClaveNoCorresponde(error);
        }
    }

    private static byte[] LeerEncabezado(Stream origen, out int longitud, long maximoCifrado)
    {
        var encabezado = new byte[LongitudEncabezado];
        LeerExacto(origen, encabezado);
        if (!encabezado.AsSpan(0, 8).SequenceEqual(Magia)) throw ErroresRespaldo.CopiaDanada("no es una copia de esta aplicación.");
        if (BinaryPrimitives.ReadUInt16LittleEndian(encabezado.AsSpan(8)) != VersionSobre
            || BinaryPrimitives.ReadUInt16LittleEndian(encabezado.AsSpan(10)) != 0)
            throw ErroresRespaldo.CopiaDanada("versión de formato no admitida.");
        var declarada = BinaryPrimitives.ReadInt64LittleEndian(encabezado.AsSpan(40));
        if (declarada <= 0 || declarada > Array.MaxLength) throw ErroresRespaldo.CopiaDanada("longitud declarada no válida.");
        if (declarada > maximoCifrado) throw ErroresRespaldo.Excede($"{maximoCifrado / (1024 * 1024)} MiB");
        longitud = (int)declarada;
        return encabezado;
    }

    private static void LeerExacto(Stream origen, Span<byte> destino)
    {
        try
        {
            origen.ReadExactly(destino);
        }
        catch (EndOfStreamException error)
        {
            throw ErroresRespaldo.CopiaDanada("el archivo está truncado.", error);
        }
    }
}
