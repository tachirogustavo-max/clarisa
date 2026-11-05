using Clarisa.Instalacion.Contratos;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Paquete;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Pasos.Comunes;

// Copia solo la parte del modo elegido. Antes de copiar, la carpeta recibe una ACL protegida en la que los usuarios solo leen y
// ejecutan: fuera de Archivos de programa la herencia suele permitirles modificar, y los servicios ejecutan estos binarios.
// Cada archivo se escribe con nombre temporal y luego se sustituye; la comprobación vuelve a calcular la huella en destino.
// Archivos ajenos al manifiesto no se borran, pero una carpeta previa con contenido que no instaló este programa no se adopta.
internal sealed class PasoPrograma : IPasoInstalacion
{
    private const string Sufijo = ".instalando";

    public string Id => "programa";
    public string Nombre => "Copia verificada de programas";
    public string Descripcion => "Protege la carpeta de programas, copia los archivos y comprueba la huella de cada uno.";
    public bool Critico => true;
    public bool Privilegiado => true;

    public async Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var destino = contexto.Plan.Solicitud.DirectorioPrograma;
        RutasSeguras.ExigirSinPuntosReparacion(destino);
        if (!Directory.Exists(destino)) return Comprobacion.NoSatisfecha("La carpeta de programas todavía no existe.");
        if (!contexto.Estado.EsPropio(TipoArtefacto.Directorio, destino))
            return Comprobacion.NoSatisfecha("La carpeta de programas no consta como creada por esta instalación.");
        if (contexto.Sistema.Permisos.Leer(destino)?.Coincide(Reglas(contexto.Plan.Modo)) != true)
            return Comprobacion.NoSatisfecha("La carpeta de programas tiene otro propietario o permisos que dejan modificarla.");
        var archivos = contexto.Paquete.Del(contexto.Plan.Modo);
        var diferencias = new List<string>();
        foreach (var archivo in archivos)
        {
            if (await ManifiestoPaquete.DiferenciaAsync(destino, archivo, ct) is { } diferencia) diferencias.Add(diferencia);
            if (diferencias.Count >= 5) break;
        }
        return diferencias.Count == 0
            ? Comprobacion.Satisfecha($"{archivos.Count} archivos en {destino} coinciden con el paquete; solo administración puede modificarlos.")
            : Comprobacion.NoSatisfecha("Destino distinto del paquete: " + string.Join("; ", diferencias));
    }

    public async Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var origen = contexto.Plan.Solicitud.OrigenPaquete;
        var destino = contexto.Plan.Solicitud.DirectorioPrograma;
        var archivos = contexto.Paquete.Del(contexto.Plan.Modo);
        RutasSeguras.ExigirSinPuntosReparacion(destino);
        var propia = contexto.Estado.EsPropio(TipoArtefacto.Directorio, destino);
        if (!propia) ExigirAdoptable(contexto, destino);
        contexto.Sistema.Permisos.Restringir(destino, Reglas(contexto.Plan.Modo));
        if (!propia) ExigirVacia(destino);
        contexto.Propio(TipoArtefacto.Directorio, destino);
        foreach (var resto in Directory.EnumerateFiles(destino, "*" + Sufijo, SearchOption.AllDirectories)) File.Delete(resto);
        foreach (var archivo in archivos)
        {
            if (await ManifiestoPaquete.DiferenciaAsync(destino, archivo, ct) is null) continue;
            var fuente = RutasSeguras.Dentro(origen, archivo.Ruta);
            var final = RutasSeguras.Dentro(destino, archivo.Ruta);
            RutasSeguras.ExigirSinPuntosReparacion(Path.GetDirectoryName(final)!);
            Directory.CreateDirectory(Path.GetDirectoryName(final)!);
            RutasSeguras.ExigirSinPuntosReparacion(final);
            var temporal = final + Sufijo;
            await using (var lectura = new FileStream(fuente, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan))
            await using (var escritura = new FileStream(temporal, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
            {
                await lectura.CopyToAsync(escritura, ct);
            }
            File.Move(temporal, final, overwrite: true);
        }
    }

    // Se repite justo antes de adoptarla: entre la planificación y este paso alguien pudo crear la carpeta y conservar su control.
    internal static void ExigirAdoptable(ContextoEjecucion contexto, string destino)
    {
        if (Directory.Exists(destino) && contexto.Sistema.Equipo.AncestroInseguro(destino) is { } motivo)
            throw new FalloInstalacionException(motivo);
        ExigirVacia(destino);
    }

    private static void ExigirVacia(string destino)
    {
        if (Directory.Exists(destino) && Directory.EnumerateFileSystemEntries(destino).Any())
            throw new FalloInstalacionException($"La carpeta {destino} ya contiene archivos que no instaló este programa. No se adopta ni se " +
                                                "borra: elige una carpeta nueva o vacía.");
    }

    internal static IReadOnlyList<ReglaAcceso> Reglas(ModoInstalacion modo)
    {
        List<ReglaAcceso> reglas =
        [
            new(IdentidadServicio.Administradores, DerechoAcceso.Control), new(IdentidadServicio.Sistema, DerechoAcceso.Control),
            new(IdentidadServicio.Usuarios, DerechoAcceso.Lectura)
        ];
        if (modo == ModoInstalacion.Servidor)
        {
            reglas.Add(new(IdentidadServicio.Sid(RutasServidor.ServicioApi), DerechoAcceso.Lectura));
            reglas.Add(new(IdentidadServicio.Sid(RutasServidor.ServicioMotor), DerechoAcceso.Lectura));
        }
        return reglas;
    }
}
