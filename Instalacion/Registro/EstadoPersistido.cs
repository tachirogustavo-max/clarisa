using System.Text.Json;

namespace Clarisa.Instalacion.Registro;

internal enum TipoArtefacto
{
    Directorio,
    Archivo,
    Servicio,
    ReglaFirewall,
    CertificadoConfianza,
    AccesoDirecto,
    BaseDatos,
    UsuarioMotor,
    // Añadidos al final: el estado guarda el número del tipo.
    RegistroAplicacion,
    Herramienta
}

// Conservar es verdadero para lo que nunca se retira por defecto: datos, claves, copias y bases.
internal sealed record ArtefactoPropio(TipoArtefacto Tipo, string Identificador, bool Conservar, DateTimeOffset Registrado);

// Marca que deja la retirada: los datos conservados pertenecen a esta versión y huella del paquete. CopiaSha256 y
// MantenimientoSha256 solo existen en el servidor. Completa distingue una retirada interrumpida.
internal sealed record RetiradaConservada(DateTimeOffset Fecha, string Version, string HuellaManifiesto, string? VersionMotor,
    string? Copia, long BytesCopia, string? CopiaSha256, string? MantenimientoSha256, bool Completa);

internal sealed class EstadoPersistido
{
    private const long MaximoBytes = 1024 * 1024;

    public int Formato { get; set; } = 1;
    public string Modo { get; set; } = "";
    public Dictionary<string, DateTimeOffset> PasosAprobados { get; set; } = new(StringComparer.Ordinal);
    public List<ArtefactoPropio> Artefactos { get; set; } = [];
    public RetiradaConservada? Retirada { get; set; }

    public static string Ruta(string directorioRegistros) => Path.Combine(directorioRegistros, "estado-instalacion.json");

    public static EstadoPersistido Cargar(string directorioRegistros, string modo)
    {
        var info = new FileInfo(Ruta(directorioRegistros));
        if (!info.Exists) return new EstadoPersistido { Modo = modo };
        if (info.Length > MaximoBytes) throw new InvalidDataException("El estado de instalación previo es demasiado grande.");
        EstadoPersistido? estado;
        try
        {
            estado = JsonSerializer.Deserialize<EstadoPersistido>(File.ReadAllBytes(info.FullName));
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("El estado de instalación previo está dañado; no se repara a ciegas.", error);
        }
        if (estado is null || estado.Formato != 1 || estado.PasosAprobados is null || estado.Artefactos is null
            || estado.Artefactos.Any(a => a is null || string.IsNullOrEmpty(a.Identificador)))
            throw new InvalidDataException("Formato de estado de instalación no admitido.");
        if (!string.Equals(estado.Modo, modo, StringComparison.Ordinal))
            throw new InvalidDataException($"Este equipo ya tiene una instalación en modo {estado.Modo}; no se cambia de modo encima.");
        return estado;
    }

    public void RegistrarArtefacto(TipoArtefacto tipo, string identificador, bool conservar, DateTimeOffset momento)
    {
        if (EsPropio(tipo, identificador)) return;
        Artefactos.Add(new ArtefactoPropio(tipo, identificador, conservar, momento));
    }

    public void OlvidarArtefacto(TipoArtefacto tipo, string identificador) => Artefactos.RemoveAll(a =>
        a.Tipo == tipo && string.Equals(a.Identificador, identificador, StringComparison.OrdinalIgnoreCase));

    public bool EsPropio(TipoArtefacto tipo, string identificador) =>
        Artefactos.Any(a => a.Tipo == tipo && string.Equals(a.Identificador, identificador, StringComparison.OrdinalIgnoreCase));

    public void Guardar(string directorioRegistros) => EscrituraSegura.Reemplazar(Ruta(directorioRegistros),
        flujo => JsonSerializer.Serialize(flujo, this, new JsonSerializerOptions { WriteIndented = true }));
}
