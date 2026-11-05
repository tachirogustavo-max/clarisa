using Clarisa.Application.Features.Respaldos;
using System.Text.RegularExpressions;

namespace Clarisa.Infrastructure.Configuration;

// Rutas absolutas en la carpeta de datos del servidor, preparadas por la instalación con permisos restringidos. La instalación otorga
// al usuario de la aplicación permisos DML sobre las bases con PrefijoBaseRecuperada; el mantenimiento no concede privilegios.
public sealed partial record ConfiguracionRespaldos
{
    public const long MiB = 1024 * 1024;

    public required string Directorio { get; init; }
    public required string ArchivoClave { get; init; }
    public required string ArchivoBaseActiva { get; init; }
    public required string ArchivoExclusion { get; init; }
    public string PrefijoBaseRecuperada { get; init; } = "liceo_rec_";
    public long MaximoBytesCifrado { get; init; } = 256 * MiB;
    public long MaximoBytesDescomprimidos { get; init; } = 1024 * MiB;
    public long MaximoFilasPorTabla { get; init; } = 5_000_000;
    public long MinimoBytesLibres { get; init; } = 512 * MiB;
    // Copia diaria del propio servidor: hora local «HH:mm» y cuántas copias automáticas se conservan.
    public bool CopiasAutomaticas { get; init; } = true;
    public string HoraCopiaAutomatica { get; init; } = "13:00";
    public int CopiasAutomaticasConservadas { get; init; } = 30;

    public ReglasCopiasAutomaticas ReglasAutomaticas() =>
        new(CopiasAutomaticas, TimeOnly.ParseExact(HoraCopiaAutomatica, "HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            CopiasAutomaticasConservadas);

    public string DirectorioOperaciones => Path.Combine(Directorio, "operaciones");
    public string DirectorioCopias => Path.Combine(Directorio, "copias");
    public string DirectorioTemporal => Path.Combine(Directorio, "temporal");

    public ConfiguracionRespaldos Validar()
    {
        foreach (var (ruta, nombre) in new[] { (Directorio, nameof(Directorio)), (ArchivoClave, nameof(ArchivoClave)),
                     (ArchivoBaseActiva, nameof(ArchivoBaseActiva)), (ArchivoExclusion, nameof(ArchivoExclusion)) })
        {
            if (string.IsNullOrWhiteSpace(ruta) || !Path.IsPathFullyQualified(ruta) || ruta.Any(char.IsControl))
                throw new ArgumentException("Indica una ruta absoluta en el servidor.", nombre);
        }
        if (Path.GetFullPath(ArchivoClave).StartsWith(Path.GetFullPath(Directorio) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("La clave no se guarda junto a las copias que protege.", nameof(ArchivoClave));
        if (!PrefijoBase().IsMatch(PrefijoBaseRecuperada))
            throw new ArgumentException("El prefijo de base usa de 1 a 20 letras minúsculas, números o guiones bajos.", nameof(PrefijoBaseRecuperada));
        if (MaximoBytesCifrado is < MiB or > 1024 * MiB)
            throw new ArgumentException("El tamaño máximo de copia debe estar entre 1 MiB y 1 GiB.", nameof(MaximoBytesCifrado));
        if (MaximoBytesDescomprimidos < MaximoBytesCifrado || MaximoBytesDescomprimidos > 8L * 1024 * MiB)
            throw new ArgumentException("El contenido descomprimido admite entre el tamaño de copia y 8 GiB.", nameof(MaximoBytesDescomprimidos));
        if (MaximoFilasPorTabla is < 1 or > 100_000_000)
            throw new ArgumentException("Usa de 1 a 100000000 filas por tabla.", nameof(MaximoFilasPorTabla));
        if (MinimoBytesLibres < 0) throw new ArgumentException("El espacio mínimo no puede ser negativo.", nameof(MinimoBytesLibres));
        if (!TimeOnly.TryParseExact(HoraCopiaAutomatica, "HH:mm", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out _))
            throw new ArgumentException("Indica la hora de la copia automática como HH:mm, por ejemplo 13:00.", nameof(HoraCopiaAutomatica));
        if (CopiasAutomaticasConservadas is < ReglasCopiasAutomaticas.MinimoConservar or > ReglasCopiasAutomaticas.MaximoConservar)
            throw new ArgumentException($"Conserva entre {ReglasCopiasAutomaticas.MinimoConservar} y {ReglasCopiasAutomaticas.MaximoConservar} copias automáticas.",
                nameof(CopiasAutomaticasConservadas));
        return this;
    }

    public void CrearDirectorios()
    {
        Directory.CreateDirectory(DirectorioOperaciones);
        Directory.CreateDirectory(DirectorioCopias);
        Directory.CreateDirectory(DirectorioTemporal);
    }

    [GeneratedRegex("^[a-z0-9_]{1,20}$")]
    private static partial Regex PrefijoBase();
}
