using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Pasos.Servidor;

// Cada carpeta de datos recibe una ACL protegida, con propietario Administradores y exactamente las identidades necesarias: los usuarios
// comunes no leen secretos ni cambian configuración. Una carpeta que ya existía con contenido que no creó esta instalación no se adopta:
// un usuario común pudo crearla antes en ProgramData y dejar ahí una configuración o una clave elegida por él.
internal sealed class PasoDirectorios : IPasoInstalacion
{
    private const int MaximoEntradasRevisadas = 10_000;

    public string Id => "directorios";
    public string Nombre => "Carpetas de datos y permisos";
    public string Descripcion => "Crea las carpetas del servidor y limita su acceso a Administradores, SYSTEM y la cuenta de cada servicio.";
    public bool Critico => true;
    public bool Privilegiado => true;

    public Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var rutas = contexto.RutasServidor;
        var diferencias = new List<string>();
        foreach (var (ruta, reglas) in Esperadas(rutas))
        {
            RutasSeguras.ExigirSinPuntosReparacion(ruta);
            if (!Directory.Exists(ruta)) diferencias.Add($"falta {ruta}");
            else if (!contexto.Estado.EsPropio(TipoArtefacto.Directorio, ruta)) diferencias.Add($"{ruta} no consta como creada por esta instalación");
            else if (contexto.Sistema.Permisos.Leer(ruta)?.Coincide(reglas) != true) diferencias.Add($"propietario o permisos distintos en {ruta}");
        }
        if (Directory.Exists(rutas.Temporal) && Directory.EnumerateFiles(rutas.Temporal).Any())
            diferencias.Add("quedan archivos temporales de una ejecución interrumpida");
        return Task.FromResult(diferencias.Count == 0
            ? Comprobacion.Satisfecha("Carpetas de datos con propietario y permisos mínimos verificados.")
            : Comprobacion.NoSatisfecha(string.Join("; ", diferencias.Take(6))));
    }

    public Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var rutas = contexto.RutasServidor;
        var esperadas = Esperadas(rutas);
        var permitidas = esperadas.Select(e => Path.GetFullPath(e.Ruta)).Append(Path.GetFullPath(contexto.Plan.DirectorioRegistros)).ToList();
        foreach (var (ruta, reglas) in esperadas)
        {
            ct.ThrowIfCancellationRequested();
            RutasSeguras.ExigirSinPuntosReparacion(ruta);
            var propia = contexto.Estado.EsPropio(TipoArtefacto.Directorio, ruta);
            if (!propia && Directory.Exists(ruta) && contexto.Sistema.Equipo.AncestroInseguro(ruta) is { } motivo)
                throw new FalloInstalacionException(motivo);
            if (!propia) ExigirSinAjenos(ruta, permitidas);
            contexto.Sistema.Permisos.Restringir(ruta, reglas);
            // Lo que se haya plantado mientras la carpeta todavía heredaba permisos de ProgramData aparece ahora.
            if (!propia) ExigirSinAjenos(ruta, permitidas);
            contexto.Propio(TipoArtefacto.Directorio, ruta, conservar: true);
        }
        foreach (var restante in Directory.EnumerateFiles(rutas.Temporal))
        {
            ConfiguracionServidor.BorrarTemporal(restante);
            contexto.Anotar(Id, $"Se eliminó un temporal de una ejecución interrumpida: {Path.GetFileName(restante)}");
        }
        return Task.CompletedTask;
    }

    private static void ExigirSinAjenos(string ruta, IReadOnlyList<string> permitidas)
    {
        if (!Directory.Exists(ruta)) return;
        var ajena = Directory.EnumerateFileSystemEntries(ruta).Take(MaximoEntradasRevisadas)
            .FirstOrDefault(e => !permitidas.Any(p => RutasSeguras.Contiene(Path.GetFullPath(e), p)));
        if (ajena is not null)
            throw new FalloInstalacionException($"La carpeta {ruta} ya contiene «{Path.GetFileName(ajena)}», que no creó esta instalación. No se " +
                                                "adopta ni se borra: revísala con el responsable técnico o elige otra carpeta de datos.");
    }

    internal static IReadOnlyList<(string Ruta, IReadOnlyList<ReglaAcceso> Reglas)> Esperadas(RutasServidor rutas)
    {
        var api = IdentidadServicio.Sid(RutasServidor.ServicioApi);
        var motor = IdentidadServicio.Sid(RutasServidor.ServicioMotor);
        IReadOnlyList<ReglaAcceso> Con(params ReglaAcceso[] adicionales) =>
            [new(IdentidadServicio.Administradores, DerechoAcceso.Control), new(IdentidadServicio.Sistema, DerechoAcceso.Control), .. adicionales];
        return
        [
            (rutas.Datos, Con(new ReglaAcceso(api, DerechoAcceso.Lectura))),
            (rutas.Configuracion, Con(new ReglaAcceso(api, DerechoAcceso.Lectura))),
            (rutas.Certificado, Con(new ReglaAcceso(api, DerechoAcceso.Lectura))),
            (rutas.Clave, Con(new ReglaAcceso(api, DerechoAcceso.Lectura))),
            (rutas.Respaldos, Con(new ReglaAcceso(api, DerechoAcceso.Modificacion))),
            (rutas.Mantenimiento, Con(new ReglaAcceso(api, DerechoAcceso.Modificacion))),
            (rutas.Registros, Con(new ReglaAcceso(api, DerechoAcceso.Modificacion))),
            (rutas.Motor, Con(new ReglaAcceso(motor, DerechoAcceso.Modificacion))),
            (rutas.Temporal, Con()),
            (rutas.Instalador, Con())
        ];
    }
}
