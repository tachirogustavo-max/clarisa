using System.ComponentModel;
using System.Security;
using Clarisa.Instalacion.Contratos;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Pasos.Comunes;
using Clarisa.Instalacion.Pasos.Servidor;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Retirada;

internal sealed record RevisionInterna(RevisionRetirada Publica, ReferenciaRetirada? Referencia, EstadoPersistido? Estado, string Carpeta,
    string Registros);

// Solo lectura. Cualquier duda sobre la propiedad de lo que se retiraría se convierte en un problema y no se propone ejecutar nada.
internal sealed class RevisorRetirada(SistemaInstalacion sistema, string registrosEstacion)
{
    public async Task<RevisionInterna> RevisarAsync(SolicitudRetirada solicitud, CancellationToken ct)
    {
        var carpeta = Path.GetDirectoryName(solicitud.Referencia)!;
        // Estación: ubicación fija de equipo. Servidor: carpeta hermana dentro de <Datos>\instalador; se contrasta después con la referencia.
        var registros = Igual(carpeta, ReferenciaRetirada.Carpeta(registrosEstacion))
            ? Path.GetFullPath(registrosEstacion)
            : Path.Combine(Path.GetDirectoryName(carpeta)!, "registros");
        var problemas = new List<ProblemaSolicitud>();
        ReferenciaRetirada? referencia = null;
        EstadoPersistido? estado = null;
        try
        {
            RutasSeguras.ExigirSinPuntosReparacion(solicitud.Referencia);
            if (!string.Equals(Path.GetFileName(carpeta), "retirada", StringComparison.OrdinalIgnoreCase))
                throw new FalloInstalacionException("La referencia no está en la carpeta de retirada de una instalación.");
            foreach (var protegida in new[] { carpeta, registros })
                if (sistema.Permisos.Leer(protegida)?.Coincide(PasoRegistroRetirada.Acceso) != true)
                    throw new FalloInstalacionException($"La carpeta {protegida} no existe o no tiene la protección de la instalación; no se confía en ella.");
            referencia = ReferenciaRetirada.Leer(solicitud.Referencia);
            var prevista = referencia.Modo == ModoInstalacion.Servidor
                ? Path.Combine(referencia.Solicitud.Datos!, "instalador", "retirada")
                : ReferenciaRetirada.Carpeta(registrosEstacion);
            if (!Igual(prevista, carpeta)) throw new FalloInstalacionException("La referencia no corresponde a la ubicación de esta instalación.");
            if (!File.Exists(EstadoPersistido.Ruta(registros))) throw new FalloInstalacionException("Falta el estado de la instalación.");
            estado = EstadoPersistido.Cargar(registros, referencia.Modo.ToString());
            if (!estado.EsPropio(TipoArtefacto.Herramienta, carpeta)) throw new FalloInstalacionException("La carpeta de retirada no consta como propia.");
            var herramienta = await PasoRegistroRetirada.HuellaAsync(Path.Combine(carpeta, ReferenciaRetirada.NombreHerramienta), ct);
            if (herramienta is not { } h || h.Bytes != referencia.HerramientaBytes || h.Sha256 != referencia.HerramientaSha256)
                problemas.Add(new("herramienta", "La herramienta protegida falta o no coincide con la registrada."));
            problemas.AddRange(ProblemasProgramas(referencia, estado, carpeta, registros));
            if (referencia.Modo == ModoInstalacion.Servidor) problemas.AddRange(ProblemasServidor(referencia, estado));
            if (EjecucionEnCurso(registros)) problemas.Add(new("ejecucion", "Hay otra ejecución del instalador en curso. Espera a que termine."));
        }
        catch (Exception error) when (error is FalloInstalacionException or IOException or UnauthorizedAccessException or InvalidDataException
                                          or SecurityException or Win32Exception)
        {
            problemas.Add(new("referencia", error.Message));
        }
        var pasos = referencia is null ? [] : CatalogoRetirada.Para(referencia, carpeta)
            .Select(p => new PasoPlaneado(p.Id, p.Nombre, p.Descripcion, p.Critico, p.Privilegiado)).ToList();
        var publica = new RevisionRetirada(referencia?.Instalacion ?? Guid.Empty, referencia?.Modo ?? ModoInstalacion.Servidor,
            referencia?.Version ?? "", referencia is null ? [] : ElementosRetirada.Describir(referencia, estado, carpeta, registros),
            referencia is null ? [] : ElementosRetirada.Advertencias(referencia), pasos, problemas, solicitud.Referencia);
        return new RevisionInterna(publica, referencia, estado, carpeta, registros);
    }

    private static IEnumerable<ProblemaSolicitud> ProblemasProgramas(ReferenciaRetirada referencia, EstadoPersistido estado, string carpeta, string registros)
    {
        var programa = referencia.Solicitud.Programa;
        if (RutaProhibida(programa) is { } motivo) yield return new("programa", motivo);
        foreach (var otra in new[] { carpeta, registros, referencia.Solicitud.Datos }.OfType<string>())
            if (RutasSeguras.Contiene(programa, otra) || RutasSeguras.Contiene(otra, programa))
                yield return new("programa", "La carpeta de programas se solapa con datos, registros o la herramienta; no se retira.");
        if (Directory.Exists(programa) && !estado.EsPropio(TipoArtefacto.Directorio, programa))
            yield return new("programa", $"La carpeta {programa} no consta como creada por esta instalación.");
    }

    private IEnumerable<ProblemaSolicitud> ProblemasServidor(ReferenciaRetirada referencia, EstadoPersistido estado)
    {
        var rutas = new RutasServidor((SolicitudServidor)referencia.Reconstruir(""));
        if (PuntoActualizacion.Leer(rutas) is { Completada: false, ProgramasRevertidos: false })
            yield return new("actualizacion", "Hay una actualización pendiente o interrumpida. Resuélvela con el instalador antes de retirar.");
        var api = sistema.Servicios.Consultar(RutasServidor.ServicioApi);
        foreach (var (nombre, definicion) in new[] { (RutasServidor.ServicioApi, PasoServicioApi.Definicion(rutas)),
                     (RutasServidor.ServicioMotor, PasoMotorServicio.Definicion(rutas)) })
        {
            if (sistema.Servicios.Consultar(nombre) is not { } actual) continue;
            var propio = estado.EsPropio(TipoArtefacto.Servicio, nombre) || Igual(actual.Ejecutable, definicion.Ejecutable);
            if (!propio) yield return new("servicio", $"El servicio {nombre} no pertenece a esta instalación; no se toca.");
            else if (PasoRetirarServicio.Modificado(actual, definicion) is { } cambio) yield return new("servicio", cambio);
        }
        if (api is not null && ConfiguracionRecuperable(rutas) is { } configuracion) yield return new("configuracion", configuracion);
    }

    // Eliminar el servicio borra sus variables protegidas: antes debe existir servidor.json con el mismo contenido.
    private string? ConfiguracionRecuperable(RutasServidor rutas)
    {
        var archivo = ConfiguracionServidor.Leer(rutas.ArchivoConfiguracion);
        if (archivo is null) return "Falta servidor.json: al quitar el servicio se perdería su configuración. Ejecuta la reparación del instalador antes de retirar.";
        var entorno = sistema.Servicios.LeerEntorno(RutasServidor.ServicioApi);
        if (entorno.Valores.Count == 0) return null;
        if (!entorno.Protegido) return "La clave del servicio no tiene la protección prevista; repare la instalación antes de retirar.";
        return ConfiguracionServidor.MismoContenido(archivo, EntornoServidor.Leer(entorno.Valores))
            ? null
            : "servidor.json y el entorno del servicio difieren; ejecuta la reparación del instalador antes de retirar.";
    }

    private static bool EjecucionEnCurso(string registros)
    {
        var bloqueo = Path.Combine(registros, "ejecucion.bloqueo");
        if (!File.Exists(bloqueo)) return false;
        try
        {
            using var _ = new FileStream(bloqueo, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    internal static string? RutaProhibida(string ruta)
    {
        var completa = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ruta));
        if (string.Equals(completa, Path.TrimEndingDirectorySeparator(Path.GetPathRoot(completa) ?? ""), StringComparison.OrdinalIgnoreCase))
            return "La carpeta de programas es la raíz de una unidad.";
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (windows.Length > 0 && RutasSeguras.Contiene(windows, completa)) return "La carpeta de programas está dentro de Windows.";
        var exactas = new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.CommonApplicationData,
            Environment.SpecialFolder.UserProfile, Environment.SpecialFolder.CommonDesktopDirectory, Environment.SpecialFolder.CommonPrograms };
        if (exactas.Select(Environment.GetFolderPath).Where(c => c.Length > 0).Any(c => Igual(c, completa)))
            return "La carpeta de programas es una carpeta del sistema compartida.";
        var usuarios = Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        return usuarios is not null && Igual(usuarios, completa) ? "La carpeta de programas es la de perfiles de usuario." : null;
    }

    internal static bool Igual(string a, string b) => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)), StringComparison.OrdinalIgnoreCase);
}
