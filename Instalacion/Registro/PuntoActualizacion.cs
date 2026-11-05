using System.Security.Cryptography;
using System.Text.Json;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Paquete;

namespace Clarisa.Instalacion.Registro;

internal sealed record ArchivoAnterior(string Ruta, long Bytes, string Sha256);
internal sealed record PuntoActualizacion(Guid Id, string Programa, string Datos, string VersionDestino, string HuellaDestino, Guid CopiaId,
    string Copia, long BytesCopia, string Sha256Copia, IReadOnlyList<ArchivoAnterior> Archivos,
    bool MigracionIniciada = false, bool Completada = false, bool ProgramasRevertidos = false)
{
    // MigracionConfirmada describe solo el último intento. EsquemaConfirmado indica que algún intento de este punto, con el mismo
    // paquete, verificó el esquema. LimpiezaPendiente sigue activa mientras la credencial temporal de migración pueda ser válida.
    public bool MigracionConfirmada { get; init; }
    public bool EsquemaConfirmado { get; init; }
    public bool LimpiezaPendiente { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HaciaDelante => EsquemaConfirmado || MigracionConfirmada;
    public static string Pendiente(RutasServidor rutas) => Path.Combine(rutas.Instalador, "actualizacion-pendiente.json");
    public string Directorio(RutasServidor rutas) => Path.Combine(rutas.Instalador, "actualizaciones", Id.ToString("N"));
    public void Guardar(RutasServidor rutas)
    {
        EscrituraSegura.Reemplazar(Pendiente(rutas), flujo => JsonSerializer.Serialize(flujo, this));
    }
    public static PuntoActualizacion? Leer(RutasServidor rutas)
    {
        var archivo = Pendiente(rutas);
        RutasSeguras.ExigirSinPuntosReparacion(archivo);
        if (!File.Exists(archivo)) return null;
        if (new FileInfo(archivo).Length > 8 * 1024 * 1024) throw new FalloInstalacionException("El punto de actualización es demasiado grande.");
        PuntoActualizacion punto;
        try { punto = JsonSerializer.Deserialize<PuntoActualizacion>(File.ReadAllBytes(archivo)) ?? throw new JsonException(); }
        catch (JsonException error) { throw new FalloInstalacionException("El punto de actualización está dañado; no se continúa.", error); }
        if (punto.Id == Guid.Empty || punto.CopiaId == Guid.Empty || punto.Archivos is null || punto.Archivos.Count is 0 or > 20_000
            || !string.Equals(Path.GetFullPath(punto.Programa), Path.GetFullPath(rutas.Programa), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFullPath(punto.Datos), Path.GetFullPath(rutas.Datos), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(punto.Copia, Path.Combine(rutas.Respaldos, "copias", punto.CopiaId.ToString("N") + ".liceo-respaldo"), StringComparison.OrdinalIgnoreCase))
            throw new FalloInstalacionException("El punto de actualización no corresponde a esta instalación.");
        foreach (var entrada in punto.Archivos)
            ExigirEntrada(entrada);
        if (punto.Archivos.Select(a => a.Ruta).Distinct(StringComparer.OrdinalIgnoreCase).Count() != punto.Archivos.Count)
            throw new FalloInstalacionException("El punto repite archivos.");
        return punto;
    }
    public async Task VerificarAsync(RutasServidor rutas, CancellationToken ct)
    {
        foreach (var entrada in Archivos)
        {
            ExigirEntrada(entrada);
            await ExigirHuellaAsync(RutasSeguras.Dentro(Directorio(rutas), entrada.Ruta), entrada.Bytes, entrada.Sha256, ct);
        }
        await ExigirHuellaAsync(Copia, BytesCopia, Sha256Copia, ct);
    }
    public static async Task<IReadOnlyList<ArchivoAnterior>> ConservarAsync(RutasServidor rutas, Guid id, CancellationToken ct)
    {
        var destino = Path.Combine(rutas.Instalador, "actualizaciones", id.ToString("N"));
        RutasSeguras.ExigirSinPuntosReparacion(destino);
        Directory.CreateDirectory(destino);
        var entradas = new List<ArchivoAnterior>();
        long total = 0;
        foreach (var (origen, relativa) in Fuentes(rutas))
        {
            ct.ThrowIfCancellationRequested();
            RutasSeguras.ExigirSinPuntosReparacion(origen);
            var bytes = new FileInfo(origen).Length;
            if (entradas.Count >= 20_000 || (total += bytes) > 8L * 1024 * 1024 * 1024)
                throw new FalloInstalacionException("La instalación anterior supera el límite de conservación.");
            var final = RutasSeguras.Dentro(destino, relativa);
            Directory.CreateDirectory(Path.GetDirectoryName(final)!);
            await using (var lectura = new FileStream(origen, FileMode.Open, FileAccess.Read, FileShare.Read))
            await using (var escritura = new FileStream(final, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
                await lectura.CopyToAsync(escritura, ct);
            await using var flujo = File.OpenRead(final);
            entradas.Add(new(relativa, bytes, Convert.ToHexString(await SHA256.HashDataAsync(flujo, ct))));
        }
        return entradas;
    }
    public async Task RevertirProgramasAsync(RutasServidor rutas, IReadOnlyList<ArchivoPaquete> nuevos, CancellationToken ct)
    {
        if (MigracionIniciada) throw new FalloInstalacionException("La migración pudo modificar datos. Recupere la copia en una base nueva antes de volver a los programas anteriores.");
        await VerificarAsync(rutas, ct);
        foreach (var entrada in Archivos)
        {
            var final = Destino(rutas, entrada.Ruta);
            var origen = RutasSeguras.Dentro(Directorio(rutas), entrada.Ruta);
            RutasSeguras.ExigirSinPuntosReparacion(final);
            Directory.CreateDirectory(Path.GetDirectoryName(final)!);
            EscrituraSegura.Reemplazar(final, escritura =>
            {
                using var lectura = File.OpenRead(origen);
                lectura.CopyTo(escritura);
            });
            await ExigirHuellaAsync(final, entrada.Bytes, entrada.Sha256, ct);
        }
        var anteriores = Archivos.Where(a => a.Ruta.StartsWith("programa/", StringComparison.Ordinal)).Select(a => a.Ruta[9..]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var nueva in nuevos.Where(r => !anteriores.Contains(r.Ruta)))
        {
            var final = RutasSeguras.Dentro(rutas.Programa, nueva.Ruta);
            RutasSeguras.ExigirSinPuntosReparacion(final);
            if (File.Exists(final))
            {
                await ExigirHuellaAsync(final, nueva.Bytes, nueva.Sha256, ct);
                File.Delete(final);
            }
        }
    }
    public static async Task ExigirHuellaAsync(string ruta, long bytes, string huella, CancellationToken ct)
    {
        RutasSeguras.ExigirSinPuntosReparacion(ruta);
        if (bytes < 0 || huella is null || huella.Length != 64 || !huella.All(Uri.IsHexDigit)
            || !File.Exists(ruta) || new FileInfo(ruta).Length != bytes) throw new FalloInstalacionException("Un archivo de recuperación falta o no coincide.");
        await using var flujo = File.OpenRead(ruta);
        if (!Convert.ToHexString(await SHA256.HashDataAsync(flujo, ct)).Equals(huella, StringComparison.OrdinalIgnoreCase))
            throw new FalloInstalacionException("La huella de un archivo de recuperación no coincide.");
    }
    private static void ExigirEntrada(ArchivoAnterior entrada)
    {
        if (entrada is null || entrada.Ruta is null || !(entrada.Ruta.StartsWith("programa/", StringComparison.Ordinal)
            || entrada.Ruta.StartsWith("datos/configuracion/", StringComparison.Ordinal) || entrada.Ruta.StartsWith("datos/certificado/", StringComparison.Ordinal)
            || entrada.Ruta is "datos/motor/my.ini" or "datos/mantenimiento/base-activa.json"))
            throw new FalloInstalacionException("El punto tiene una ruta no admitida.");
    }
    private static string Destino(RutasServidor rutas, string relativa) => relativa.StartsWith("programa/", StringComparison.Ordinal)
        ? RutasSeguras.Dentro(rutas.Programa, relativa[9..]) : RutasSeguras.Dentro(rutas.Datos, relativa[6..]);
    private static IEnumerable<(string Origen, string Relativa)> Fuentes(RutasServidor rutas)
    {
        foreach (var archivo in ArchivosSinEnlaces(rutas.Programa))
            yield return (archivo, "programa/" + Path.GetRelativePath(rutas.Programa, archivo).Replace('\\', '/'));
        foreach (var carpeta in new[] { rutas.Configuracion, rutas.Certificado })
            if (Directory.Exists(carpeta))
                foreach (var archivo in ArchivosSinEnlaces(carpeta))
                    yield return (archivo, "datos/" + Path.GetRelativePath(rutas.Datos, archivo).Replace('\\', '/'));
        foreach (var archivo in new[] { rutas.ArchivoMotorIni, rutas.ArchivoBaseActiva })
            if (File.Exists(archivo)) yield return (archivo, "datos/" + Path.GetRelativePath(rutas.Datos, archivo).Replace('\\', '/'));
    }
    private static IEnumerable<string> ArchivosSinEnlaces(string raiz)
    {
        var pendientes = new Stack<string>();
        pendientes.Push(raiz);
        var revisadas = 0;
        while (pendientes.TryPop(out var carpeta))
            foreach (var entrada in Directory.EnumerateFileSystemEntries(carpeta))
            {
                if (++revisadas > 50_000) throw new FalloInstalacionException("La instalación anterior contiene demasiadas entradas.");
                var atributos = File.GetAttributes(entrada);
                if (atributos.HasFlag(FileAttributes.ReparsePoint)) throw new FalloInstalacionException("No se conserva una instalación con enlaces o junctions.");
                if (atributos.HasFlag(FileAttributes.Directory)) pendientes.Push(entrada);
                else yield return entrada;
            }
    }
}
