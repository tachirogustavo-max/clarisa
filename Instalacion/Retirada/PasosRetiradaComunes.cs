using Clarisa.Instalacion.Contratos;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Pasos.Comunes;
using Clarisa.Instalacion.Registro;

namespace Clarisa.Instalacion.Retirada;

// Borra solo archivos del inventario cuya huella coincide con una versión instalada, sin seguir enlaces y con la ACL de la carpeta
// intacta (si no, alguien sin administración pudo sustituir algo). Carpetas: solo si quedan vacías. Nunca se borra por patrón.
internal sealed class PasoRetirarProgramas(ReferenciaRetirada referencia) : IPasoInstalacion
{
    public string Id => "programas";
    public string Nombre => "Retirar los programas";
    public string Descripcion => "Elimina los archivos instalados que no cambiaron; conserva y enumera lo ajeno o modificado.";
    public bool Critico => true;
    public bool Privilegiado => true;

    private string Programa => referencia.Solicitud.Programa;

    public async Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        RutasSeguras.ExigirSinPuntosReparacion(Programa);
        if (!Directory.Exists(Programa)) return Comprobacion.Satisfecha($"La carpeta {Programa} ya no existe.");
        if (contexto.Sistema.Permisos.Leer(Programa)?.Coincide(PasoPrograma.Reglas(contexto.Plan.Modo)) != true)
            return Comprobacion.Indeterminada($"Los permisos de {Programa} ya no son los instalados: no se borra nada dentro. Restáurelos o " +
                                              "retira la carpeta manualmente tras revisarla; después reintenta.");
        var propios = 0;
        foreach (var entrada in referencia.Inventario)
            if (await PropioAsync(entrada, ct) is not null) propios++;
        var restantes = Directory.EnumerateFileSystemEntries(Programa, "*", SearchOption.AllDirectories).Take(10_000).Count();
        if (propios > 0) return Comprobacion.NoSatisfecha($"Quedan {propios} archivos instalados.");
        return Comprobacion.Satisfecha(restantes == 0
            ? $"Programas retirados de {Programa}."
            : $"Programas retirados. Se conservan {restantes} entradas ajenas o modificadas en {Programa}; revísalas antes de borrarlas a mano.");
    }

    public async Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        if (contexto.Sistema.Permisos.Leer(Programa)?.Coincide(PasoPrograma.Reglas(contexto.Plan.Modo)) != true)
            throw new FalloInstalacionException($"Los permisos de {Programa} cambiaron; no se borra nada dentro.");
        foreach (var entrada in referencia.Inventario)
        {
            ct.ThrowIfCancellationRequested();
            if (await PropioAsync(entrada, ct) is { } ruta) File.Delete(ruta);
        }
        var carpetas = referencia.Inventario.Select(e => Path.GetDirectoryName(RutasSeguras.Dentro(Programa, e.Ruta))!)
            .SelectMany(Superiores).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(c => c.Length);
        foreach (var carpeta in carpetas) BorrarSiVacia(carpeta);
        BorrarSiVacia(Programa);
        if (!Directory.Exists(Programa))
        {
            contexto.Estado.OlvidarArtefacto(TipoArtefacto.Directorio, Programa);
            contexto.Estado.Guardar(contexto.Plan.DirectorioRegistros);
        }
    }

    private async Task<string?> PropioAsync(EntradaInventario entrada, CancellationToken ct)
    {
        var ruta = RutasSeguras.Dentro(Programa, entrada.Ruta);
        RutasSeguras.ExigirSinPuntosReparacion(ruta);
        return await PasoRegistroRetirada.HuellaAsync(ruta, ct) is { } actual
               && entrada.Versiones.Any(v => v.Bytes == actual.Bytes && string.Equals(v.Sha256, actual.Sha256, StringComparison.OrdinalIgnoreCase))
            ? ruta
            : null;
    }

    private IEnumerable<string> Superiores(string carpeta)
    {
        for (var actual = carpeta; RutasSeguras.Contiene(Programa, actual) && !string.Equals(Path.GetFullPath(actual),
                 Path.GetFullPath(Programa), StringComparison.OrdinalIgnoreCase); actual = Path.GetDirectoryName(actual)!)
            yield return actual;
    }

    private static void BorrarSiVacia(string carpeta)
    {
        RutasSeguras.ExigirSinPuntosReparacion(carpeta);
        if (Directory.Exists(carpeta) && !Directory.EnumerateFileSystemEntries(carpeta).Any()) Directory.Delete(carpeta, recursive: false);
    }
}

internal sealed class PasoRetirarAccesos(ReferenciaRetirada referencia) : IPasoInstalacion
{
    public string Id => "accesos-directos";
    public string Nombre => "Retirar los accesos directos";
    public string Descripcion => "Elimina los accesos directos que creó la instalación y siguen apuntando a la aplicación; conserva los cambiados.";
    public bool Critico => true;
    public bool Privilegiado => true;

    public Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var (propios, cambiados) = Clasificar(contexto);
        var detalle = cambiados.Count == 0 ? "" : " Se conservan por estar cambiados: " + string.Join(", ", cambiados);
        return Task.FromResult(propios.Count == 0
            ? Comprobacion.Satisfecha("No quedan accesos directos propios." + detalle)
            : Comprobacion.NoSatisfecha($"Quedan {propios.Count} accesos directos propios."));
    }

    public Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        foreach (var acceso in Clasificar(contexto).Propios)
        {
            RutasSeguras.ExigirSinPuntosReparacion(acceso);
            File.Delete(acceso);
            contexto.Estado.OlvidarArtefacto(TipoArtefacto.AccesoDirecto, acceso);
        }
        contexto.Estado.Guardar(contexto.Plan.DirectorioRegistros);
        return Task.CompletedTask;
    }

    private (List<string> Propios, List<string> Cambiados) Clasificar(ContextoEjecucion contexto)
    {
        var carpeta = Path.Combine(referencia.Solicitud.Programa, "estacion");
        string[] destinos = [Path.Combine(carpeta, "Clarisa.Desktop.exe"), Path.Combine(carpeta, ProgramasAnteriores.Escritorio)];
        var propios = new List<string>();
        var cambiados = new List<string>();
        foreach (var acceso in contexto.Estado.Artefactos.Where(a => a.Tipo == TipoArtefacto.AccesoDirecto).Select(a => a.Identificador))
        {
            if (!File.Exists(acceso)) continue;
            var leido = contexto.Sistema.Accesos.Leer(acceso);
            if (leido is { } l && ProgramasAnteriores.MismoArchivo(l.Destino, destinos)
                && l.Argumentos.StartsWith("--servidor ", StringComparison.Ordinal)) propios.Add(acceso);
            else cambiados.Add(acceso);
        }
        return (propios, cambiados);
    }
}

internal sealed class PasoConservarConfianza : IPasoInstalacion
{
    public string Id => "confianza-certificado";
    public string Nombre => "Certificado de confianza del servidor";
    public string Descripcion => "Se conserva: no se puede demostrar que ningún otro uso del equipo dependa de él.";
    public bool Critico => false;
    public bool Privilegiado => false;

    public Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var huellas = contexto.Estado.Artefactos.Where(a => a.Tipo == TipoArtefacto.CertificadoConfianza).Select(a => a.Identificador).ToList();
        return Task.FromResult(Comprobacion.Omitida(huellas.Count == 0
            ? "Este equipo no añadió certificados de confianza."
            : "Se conserva en Raíces de confianza del equipo el certificado del servidor (huella SHA-256 " + string.Join(", ", huellas) +
              "). No es de autoridad y solo autentica a ese servidor; un administrador puede retirarlo si ningún otro uso lo necesita."));
    }

    public Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct) => Task.CompletedTask;
}
