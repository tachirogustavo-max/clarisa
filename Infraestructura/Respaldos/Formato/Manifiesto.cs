using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Clarisa.Application.Features.Respaldos;

namespace Clarisa.Infrastructure.Respaldos.Formato;

internal sealed record MigracionCopiada(int Version, string Huella);

internal sealed record TablaManifiesto(string Nombre, IReadOnlyList<string> Columnas, long Filas, string Sha256);

internal sealed record Manifiesto(int Formato, string Aplicacion, DateTimeOffset CreadoUtc, IReadOnlyList<MigracionCopiada> Migraciones,
    IReadOnlyList<TablaManifiesto> Tablas)
{
    public const string Entrada = "manifiesto.json";
    // Marca del formato dentro de cada copia: las copias existentes la llevan y las versiones anteriores la exigen al restaurar.
    public const string NombreAplicacion = "Escolar.Liceo";
    public const int MaximoBytes = 1024 * 1024;

    private static readonly JsonSerializerOptions Opciones = new() { WriteIndented = true, MaxDepth = 8 };

    public byte[] Serializar() => JsonSerializer.SerializeToUtf8Bytes(this, Opciones);

    public static Manifiesto Leer(ReadOnlySpan<byte> contenido)
    {
        Manifiesto? manifiesto;
        try
        {
            manifiesto = JsonSerializer.Deserialize<Manifiesto>(contenido, Opciones);
        }
        catch (JsonException error)
        {
            throw ErroresRespaldo.CopiaDanada("el manifiesto no es válido.", error);
        }
        if (manifiesto is not { Formato: CatalogoRespaldo.Formato, Aplicacion: NombreAplicacion, Migraciones: not null, Tablas: not null })
            throw ErroresRespaldo.CopiaDanada("el manifiesto no corresponde a este formato de copia.");
        if (manifiesto.Tablas.Count != CatalogoRespaldo.Tablas.Count)
            throw ErroresRespaldo.CopiaDanada("el manifiesto no contiene las tablas esperadas.");
        for (var indice = 0; indice < manifiesto.Tablas.Count; indice++)
        {
            var (declarada, conocida) = (manifiesto.Tablas[indice], CatalogoRespaldo.Tablas[indice]);
            if (declarada is null || declarada.Nombre != conocida.Nombre || declarada.Columnas is null
                || !declarada.Columnas.SequenceEqual(conocida.Columnas.Select(c => c.Nombre)) || declarada.Filas < 0
                || declarada.Sha256 is not { Length: 64 } || !declarada.Sha256.All(Uri.IsHexDigit))
                throw ErroresRespaldo.CopiaDanada($"la tabla {indice + 1} del manifiesto no coincide con el catálogo.");
        }
        if (manifiesto.Migraciones.Count is 0 or > 1000 || manifiesto.Migraciones.Any(m => m is null || m.Huella is not { Length: 64 }))
            throw ErroresRespaldo.CopiaDanada("la lista de migraciones no es válida.");
        return manifiesto;
    }

    public ResumenCopia Resumir() =>
        new(CreadoUtc, Migraciones.Max(m => m.Version), Tablas.Select(t => new TablaCopia(t.Nombre, t.Filas)).ToList(),
            HuellaContenido(Tablas.Select(t => (t.Nombre, t.Filas, t.Sha256))));

    // Resume tablas, recuentos y huellas en un solo valor; la base recuperada se vuelve a leer y debe producir exactamente el mismo.
    public static string HuellaContenido(IEnumerable<(string Nombre, long Filas, string Sha256)> tablas)
    {
        var texto = new StringBuilder();
        foreach (var (nombre, filas, sha) in tablas) texto.Append(nombre).Append('\t').Append(filas).Append('\t').Append(sha).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto.ToString())));
    }
}
