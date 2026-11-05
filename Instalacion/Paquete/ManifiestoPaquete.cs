using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Clarisa.Instalacion.Contratos;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Registro;

namespace Clarisa.Instalacion.Paquete;

internal sealed record ArchivoPaquete(string Ruta, long Bytes, string Sha256);

// El manifiesto solo acredita integridad respecto de sí mismo: su autenticidad depende del instalador firmado que lo transporta.
internal sealed partial record ManifiestoPaquete(int Formato, string Version, IReadOnlyList<ArchivoPaquete> Archivos)
{
    public string HuellaContenido { get; init; } = "";
    public const string NombreArchivo = "manifiesto-instalacion.json";
    private const long MaximoBytes = 8 * 1024 * 1024;
    private const int MaximoArchivos = 20_000;
    private const int MaximoRuta = 200;
    private const long MaximoTotal = 8L * 1024 * 1024 * 1024;
    private const string Licencias = "licencias/";

    public static readonly IReadOnlyDictionary<ModoInstalacion, string[]> Partes = new Dictionary<ModoInstalacion, string[]>
    {
        [ModoInstalacion.Servidor] = ["servidor/", "motor/", Licencias],
        [ModoInstalacion.Estacion] = ["estacion/", Licencias]
    };

    public static readonly IReadOnlyDictionary<ModoInstalacion, string[]> Obligatorios = new Dictionary<ModoInstalacion, string[]>
    {
        [ModoInstalacion.Servidor] = ["servidor/Clarisa.Api.exe", "servidor/Clarisa.Mantenimiento.exe", "servidor/Resources/claves-prohibidas.txt",
            "motor/bin/mariadbd.exe", "motor/bin/mariadb-install-db.exe", "motor/COPYING", "motor/CREDITS", "motor/THIRDPARTY"],
        [ModoInstalacion.Estacion] = ["estacion/Clarisa.Desktop.exe"]
    };

    private static readonly string[] Raices = ["servidor/", "estacion/", "motor/", Licencias];
    private static readonly string[] Reservados = ["CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
        .. Enumerable.Range(0, 10).SelectMany(n => new[] { $"COM{n}", $"LPT{n}" })];

    public static ManifiestoPaquete Leer(string origen, string? huellaEsperada = null)
    {
        var info = new FileInfo(Path.Combine(origen, NombreArchivo));
        if (!info.Exists) throw new FalloInstalacionException("El paquete no tiene manifiesto-instalacion.json; no se puede comprobar su integridad.");
        if (info.Length > MaximoBytes) throw new FalloInstalacionException("El manifiesto del paquete es demasiado grande.");
        ManifiestoPaquete? manifiesto;
        var contenido = File.ReadAllBytes(info.FullName);
        if (huellaEsperada is not null && !string.Equals(Convert.ToHexString(SHA256.HashData(contenido)), huellaEsperada, StringComparison.OrdinalIgnoreCase))
            throw new FalloInstalacionException("El manifiesto no corresponde al instalador recibido. No se usa este paquete.");
        try
        {
            manifiesto = JsonSerializer.Deserialize<ManifiestoPaquete>(contenido);
        }
        catch (JsonException)
        {
            throw new FalloInstalacionException("El manifiesto del paquete no es JSON válido.");
        }
        if (manifiesto is null || manifiesto.Formato != 1 || manifiesto.Archivos is null || manifiesto.Archivos.Count > MaximoArchivos
            || manifiesto.Version is null || !PatronVersion().IsMatch(manifiesto.Version))
            throw new FalloInstalacionException("Formato de manifiesto no admitido.");
        foreach (var archivo in manifiesto.Archivos)
        {
            if (archivo is null) throw new FalloInstalacionException("El manifiesto tiene una entrada vacía.");
            if (ProblemaRuta(archivo.Ruta) is { } problema) throw new FalloInstalacionException($"Entrada de manifiesto no admitida: {problema}.");
            if (archivo.Bytes < 0 || archivo.Sha256 is null || !PatronHuella().IsMatch(archivo.Sha256))
                throw new FalloInstalacionException($"Entrada inválida: {archivo.Ruta}");
            RutasSeguras.Dentro(origen, archivo.Ruta);
        }
        if (manifiesto.Archivos.Select(a => a.Ruta).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifiesto.Archivos.Count)
            throw new FalloInstalacionException("El manifiesto repite archivos.");
        if (manifiesto.Archivos.Sum(a => a.Bytes) > MaximoTotal) throw new FalloInstalacionException("El paquete declara un tamaño no admitido.");
        return manifiesto with { HuellaContenido = Convert.ToHexString(SHA256.HashData(contenido)) };
    }

    // Solo raíces conocidas y nombres válidos de Windows: sin flujos alternos (':'), dispositivos reservados ni puntos/espacios finales.
    internal static string? ProblemaRuta(string? ruta)
    {
        if (string.IsNullOrEmpty(ruta) || ruta.Length > MaximoRuta || ruta.Contains('\\')) return $"ruta no válida «{ruta}»";
        if (!Raices.Any(r => ruta.StartsWith(r, StringComparison.Ordinal))) return $"{ruta} está fuera de servidor/, estacion/, motor/ y licencias/";
        foreach (var parte in ruta.Split('/'))
        {
            if (parte.Length == 0 || parte is "." or ".." || parte.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || parte.EndsWith('.') || parte.EndsWith(' ')
                || Reservados.Contains(parte.Split('.')[0], StringComparer.OrdinalIgnoreCase))
                return $"{ruta} tiene un nombre no admitido";
        }
        return null;
    }

    public IReadOnlyList<ArchivoPaquete> Del(ModoInstalacion modo) =>
        Archivos.Where(a => Partes[modo].Any(p => a.Ruta.StartsWith(p, StringComparison.Ordinal))).ToList();

    public IReadOnlyList<string> Faltantes(ModoInstalacion modo)
    {
        var faltan = Obligatorios[modo].Where(o => !Archivos.Any(a => a.Ruta == o)).ToList();
        if (!Archivos.Any(a => a.Ruta.StartsWith(Licencias, StringComparison.Ordinal))) faltan.Add(Licencias + " (licencias de terceros)");
        return faltan;
    }

    public static async Task<string?> DiferenciaAsync(string raiz, ArchivoPaquete archivo, CancellationToken ct)
    {
        var ruta = RutasSeguras.Dentro(raiz, archivo.Ruta);
        var info = new FileInfo(ruta);
        if (!info.Exists) return $"falta {archivo.Ruta}";
        if (info.Attributes.HasFlag(FileAttributes.ReparsePoint)) return $"{archivo.Ruta} es un enlace";
        if (info.Length != archivo.Bytes) return $"{archivo.Ruta} tiene otro tamaño";
        await using var flujo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);
        var huella = Convert.ToHexString(await SHA256.HashDataAsync(flujo, ct));
        return string.Equals(huella, archivo.Sha256, StringComparison.OrdinalIgnoreCase) ? null : $"{archivo.Ruta} fue alterado";
    }

    [GeneratedRegex("^[0-9A-Za-z][0-9A-Za-z.+-]{0,39}$")]
    private static partial Regex PatronVersion();

    [GeneratedRegex("^[0-9A-Fa-f]{64}$")]
    private static partial Regex PatronHuella();
}
