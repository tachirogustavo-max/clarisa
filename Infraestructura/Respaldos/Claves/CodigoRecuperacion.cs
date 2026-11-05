using System.Security.Cryptography;
using System.Text;
using Clarisa.Application.Common.Errores;

namespace Clarisa.Infrastructure.Respaldos.Claves;

// Recurso portátil de la clave: texto legible (Base32 en grupos) con suma de control para detectar errores de transcripción. Es tan
// sensible como la propia clave; se guarda fuera del servidor. La suma de control no autentica nada: solo detecta errores de copia.
internal static class CodigoRecuperacion
{
    // Encabezado de los archivos de recuperación ya entregados al liceo; las versiones anteriores lo exigen al importarlos.
    public const string Formato = "ESCOLAR-RECUPERACION-1";
    private const string Alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int LongitudControl = 4;
    private const int MaximoBytesArchivo = 8 * 1024;

    public static string Componer(ClaveRespaldo clave)
    {
        var datos = new byte[ClaveRespaldo.Longitud + LongitudControl];
        try
        {
            clave.Material.CopyTo(datos);
            Control(clave.Id, clave.Material).CopyTo(datos.AsSpan(ClaveRespaldo.Longitud));
            var codigo = Base32(datos);
            var grupos = Enumerable.Range(0, (codigo.Length + 4) / 5).Select(i => codigo.Substring(i * 5, Math.Min(5, codigo.Length - i * 5)));
            return $"""
                # Clave de recuperación de las copias de seguridad del liceo.
                # Guárdela fuera del servidor (impresa o en una memoria resguardada). Quien la tenga puede leer las copias.
                # Sin esta clave, las copias no se pueden recuperar si se pierde el servidor.
                FORMATO: {Formato}
                ID: {clave.Id:D}
                CODIGO: {string.Join('-', grupos)}

                """;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(datos);
        }
    }

    public static ClaveRespaldo LeerArchivo(string ruta)
    {
        var info = new FileInfo(ruta);
        if (!info.Exists)
            throw new ErrorAplicacionException(TipoError.NoEncontrado,
                "No se encontró el archivo de recuperación indicado.", "ClaveRecuperacion");
        if (info.Length > MaximoBytesArchivo) throw Invalido();
        return Interpretar(File.ReadAllText(ruta, Encoding.UTF8));
    }

    public static ClaveRespaldo Interpretar(string texto)
    {
        var campos = texto.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => l.Split(':', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0].Trim().ToUpperInvariant(), p => p[1].Trim());
        if (campos.GetValueOrDefault("FORMATO") != Formato || !Guid.TryParseExact(campos.GetValueOrDefault("ID"), "D", out var id)
            || campos.GetValueOrDefault("CODIGO") is not { } codigo)
            throw Invalido();
        var datos = DesdeBase32(codigo.Replace("-", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant());
        try
        {
            if (datos is not { Length: ClaveRespaldo.Longitud + LongitudControl }
                || !CryptographicOperations.FixedTimeEquals(datos.AsSpan(ClaveRespaldo.Longitud),
                    Control(id, datos.AsSpan(0, ClaveRespaldo.Longitud))))
                throw Invalido();
            return new ClaveRespaldo(id, datos.AsSpan(0, ClaveRespaldo.Longitud));
        }
        finally
        {
            if (datos is not null) CryptographicOperations.ZeroMemory(datos);
        }
    }

    private static byte[] Control(Guid id, ReadOnlySpan<byte> material)
    {
        using var huella = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        huella.AppendData(Encoding.ASCII.GetBytes(Formato));
        huella.AppendData(id.ToByteArray());
        huella.AppendData(material);
        return huella.GetHashAndReset()[..LongitudControl];
    }

    private static string Base32(ReadOnlySpan<byte> datos)
    {
        var texto = new StringBuilder();
        int acumulado = 0, bits = 0;
        foreach (var octeto in datos)
        {
            acumulado = (acumulado << 8) | octeto;
            bits += 8;
            while (bits >= 5)
            {
                texto.Append(Alfabeto[(acumulado >> (bits - 5)) & 31]);
                bits -= 5;
            }
            acumulado &= (1 << bits) - 1;
        }
        if (bits > 0) texto.Append(Alfabeto[(acumulado << (5 - bits)) & 31]);
        return texto.ToString();
    }

    private static byte[]? DesdeBase32(string texto)
    {
        var resultado = new List<byte>(texto.Length * 5 / 8);
        int acumulado = 0, bits = 0;
        foreach (var caracter in texto)
        {
            var valor = Alfabeto.IndexOf(caracter, StringComparison.Ordinal);
            if (valor < 0) return null;
            acumulado = (acumulado << 5) | valor;
            bits += 5;
            if (bits < 8) continue;
            resultado.Add((byte)(acumulado >> (bits - 8)));
            bits -= 8;
            acumulado &= (1 << bits) - 1;
        }
        return resultado.ToArray();
    }

    private static ErrorAplicacionException Invalido() =>
        new(TipoError.DatosInvalidos,
            "El archivo de recuperación no es válido o tiene errores de transcripción.", "ClaveRecuperacion");
}
