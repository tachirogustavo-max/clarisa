using System.Text.Json;

namespace Clarisa.Infrastructure.Configuration;

public sealed class ConfiguracionServidorException(string mensaje) : Exception(mensaje);

// Archivo protegido que comparten el servidor y la herramienta de mantenimiento. Se lee una sola vez y ambos interpretan los mismos bytes.
// Los mensajes nunca incluyen valores del archivo: pueden contener claves de la base.
public sealed class ArchivoConfiguracionServidor
{
    public const int MaximoBytes = 256 * 1024;
    private const int ProfundidadMaxima = 32;

    private static readonly JsonDocumentOptions Opciones = new()
    {
        CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, MaxDepth = ProfundidadMaxima
    };

    private ArchivoConfiguracionServidor(string ruta, byte[] contenido)
    {
        Ruta = ruta;
        Contenido = contenido;
    }

    public string Ruta { get; }
    public byte[] Contenido { get; }

    public static ArchivoConfiguracionServidor Leer(string? ruta, string directorioPrograma)
    {
        var completa = ExigirRuta(ruta, directorioPrograma);
        var contenido = LeerAcotado(completa);
        Validar(contenido);
        return new(completa, contenido);
    }

    public JsonDocument Documento() => JsonDocument.Parse(Contenido, Opciones);

    internal static ArchivoConfiguracionServidor DesdeEntorno(byte[] contenido)
    {
        if (contenido.Length is 0 or > MaximoBytes) throw new ConfiguracionServidorException("Tamaño de configuración del entorno no admitido.");
        Validar(contenido);
        return new("entorno del servicio", contenido);
    }

    private static string ExigirRuta(string? ruta, string directorioPrograma)
    {
        if (string.IsNullOrWhiteSpace(ruta) || ruta.Any(char.IsControl) || !Path.IsPathFullyQualified(ruta) || ruta.StartsWith(@"\\", StringComparison.Ordinal))
            throw new ConfiguracionServidorException("Indica la ruta absoluta y local del archivo de configuración del servidor.");
        var completa = Path.GetFullPath(ruta);
        foreach (var protegida in new[] { directorioPrograma, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
        {
            if (string.IsNullOrEmpty(protegida)) continue;
            var carpeta = Path.TrimEndingDirectorySeparator(Path.GetFullPath(protegida)) + Path.DirectorySeparatorChar;
            if (completa.StartsWith(carpeta, StringComparison.OrdinalIgnoreCase))
                throw new ConfiguracionServidorException("La configuración del servidor debe estar en su carpeta de datos protegida, no junto al programa.");
        }
        return completa;
    }

    private static byte[] LeerAcotado(string ruta)
    {
        try
        {
            var info = new FileInfo(ruta);
            if (!info.Exists) throw new ConfiguracionServidorException("No se encontró el archivo de configuración del servidor indicado.");
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new ConfiguracionServidorException("El archivo de configuración no puede ser un enlace.");
            using var flujo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.SequentialScan);
            var bufer = new byte[MaximoBytes + 1];
            var leidos = 0;
            int bloque;
            while (leidos < bufer.Length && (bloque = flujo.Read(bufer, leidos, bufer.Length - leidos)) > 0) leidos += bloque;
            if (leidos == 0) throw new ConfiguracionServidorException("El archivo de configuración del servidor está vacío.");
            if (leidos > MaximoBytes) throw new ConfiguracionServidorException("El archivo de configuración del servidor es demasiado grande.");
            return bufer[..leidos];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new ConfiguracionServidorException(
                $"No se pudo leer el archivo de configuración del servidor ({error.GetType().Name}). Revisa sus permisos.");
        }
    }

    private static void Validar(byte[] contenido)
    {
        try
        {
            using var documento = JsonDocument.Parse(contenido, Opciones);
            if (documento.RootElement.ValueKind != JsonValueKind.Object)
                throw new ConfiguracionServidorException("La configuración del servidor debe ser un objeto JSON.");
            ExigirNombresUnicos(documento.RootElement);
        }
        catch (JsonException)
        {
            throw new ConfiguracionServidorException("El archivo de configuración del servidor no es JSON válido.");
        }
    }

    // La configuración de .NET no distingue mayúsculas y se queda con el último valor repetido; aquí se rechaza la ambigüedad.
    private static void ExigirNombresUnicos(JsonElement elemento)
    {
        if (elemento.ValueKind == JsonValueKind.Array)
        {
            foreach (var hijo in elemento.EnumerateArray()) ExigirNombresUnicos(hijo);
            return;
        }
        if (elemento.ValueKind != JsonValueKind.Object) return;
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var propiedad in elemento.EnumerateObject())
        {
            if (!vistos.Add(propiedad.Name))
                throw new ConfiguracionServidorException("La configuración del servidor repite una propiedad en la misma sección.");
            ExigirNombresUnicos(propiedad.Value);
        }
    }
}
