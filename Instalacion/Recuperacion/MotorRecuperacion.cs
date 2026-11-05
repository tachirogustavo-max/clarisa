using System.Text.RegularExpressions;
using Clarisa.Instalacion.Contratos;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Pasos.Servidor;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Recuperacion;

// Recupera una copia de seguridad en el servidor sin usar la consola. Hace lo mismo que los comandos «verificar», «preparar» y
// «activar» de la herramienta de mantenimiento, en ese orden: la copia se carga en una base NUEVA y la actual se conserva, así que
// siempre se puede volver a ella con «revertir». El servicio se detiene solo mientras dura la carga.
public sealed partial class MotorRecuperacion
{
    private static readonly TimeSpan LimiteMantenimiento = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan LimiteServicio = TimeSpan.FromSeconds(90);
    private readonly SistemaInstalacion sistema;

    internal MotorRecuperacion(SistemaInstalacion sistema) => this.sistema = sistema;

    // Carpeta donde el servidor guarda sus copias, para abrir allí el selector de archivos. Nula si no hay un servidor instalado.
    public string? CarpetaCopias()
    {
        try { return Path.Combine(InstalacionServidor.Localizar(sistema).Rutas.Respaldos, "copias"); }
        catch (FalloInstalacionException) { return null; }
    }

    public async Task<RevisionRecuperacion> RevisarAsync(SolicitudRecuperacion solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        try
        {
            using var sesion = InstalacionServidor.Abrir(sistema, "revision");
            var salida = await MantenimientoAsync(sesion.Contexto, "verificar", null, ct, Opciones(solicitud));
            var resumen = salida.Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0).ToList();
            return new(true, "La copia se descifró y se comprobó completa. Todavía no se cambió nada.", resumen);
        }
        catch (FalloInstalacionException error)
        {
            return new(false, error.Message, []);
        }
    }

    public async Task<ResultadoRecuperacion> RecuperarAsync(SolicitudRecuperacion solicitud, CredencialOperador credencial,
        IProgress<string>? avance, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(credencial);
        var pasos = new List<string>();
        InstalacionServidor? sesion = null;
        var detenida = false;
        var activada = false;
        try
        {
            sesion = InstalacionServidor.Abrir(sistema, "recuperacion");
            var contexto = sesion.Contexto;
            contexto.Filtro.Registrar(credencial.ClaveTexto());
            void Anotar(string paso) { pasos.Add(paso); avance?.Report(paso); contexto.Anotar("recuperacion", paso); }

            await MantenimientoAsync(contexto, "verificar", null, ct, Opciones(solicitud));
            Anotar("Copia verificada de nuevo antes de empezar.");
            if (ServiciosPropios.EnEjecucion(ServiciosPropios.ConsultarPropio(contexto, RutasServidor.ServicioApi)))
            {
                await sistema.Servicios.DetenerAsync(RutasServidor.ServicioApi, LimiteServicio, ct);
                detenida = true;
                Anotar("Servidor detenido mientras se carga la copia.");
            }
            var preparada = await MantenimientoAsync(contexto, "preparar", Entrada(credencial), ct, Opciones(solicitud));
            var id = IdRecuperacion().Match(preparada) is { Success: true } coincidencia ? coincidencia.Groups[1].Value
                : throw new FalloInstalacionException("La herramienta de mantenimiento no confirmó la base recuperada. No se cambió la base en uso.");
            Anotar("Copia cargada y comprobada en una base nueva. La base en uso no se tocó.");
            await MantenimientoAsync(contexto, "activar", "ACTIVAR\n" + Entrada(credencial), ct, "--recuperacion", id);
            activada = true;
            Anotar("Base recuperada activada. La base anterior se conserva.");
            await sistema.Servicios.IniciarAsync(RutasServidor.ServicioApi, LimiteServicio, CancellationToken.None);
            detenida = false;
            Anotar("Servidor en marcha con la información recuperada.");
            return new(true, "Recuperación terminada. Las estaciones deben volver a iniciar sesión. Si algo no está bien, usa «Volver a la " +
                             "información anterior».", pasos, sesion.Ruta);
        }
        catch (Exception error) when (error is FalloInstalacionException or OperationCanceledException or IOException
                                          or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            var mensaje = error is OperationCanceledException ? "Recuperación cancelada." : error.Message;
            if (detenida) mensaje += " " + await ReanudarAsync(activada);
            else if (!activada) mensaje += " La base en uso no cambió.";
            return new(false, mensaje, pasos, sesion?.Ruta);
        }
        finally
        {
            sesion?.Dispose();
        }
    }

    // Vuelve a la base que estaba en uso antes de la última recuperación activada.
    public async Task<ResultadoRecuperacion> VolverAnteriorAsync(CredencialOperador credencial, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(credencial);
        try
        {
            using var sesion = InstalacionServidor.Abrir(sistema, "reversion");
            sesion.Contexto.Filtro.Registrar(credencial.ClaveTexto());
            if (ServiciosPropios.EnEjecucion(ServiciosPropios.ConsultarPropio(sesion.Contexto, RutasServidor.ServicioApi)))
                await sistema.Servicios.DetenerAsync(RutasServidor.ServicioApi, LimiteServicio, ct);
            await MantenimientoAsync(sesion.Contexto, "revertir", "REVERTIR\n" + Entrada(credencial), ct);
            await sistema.Servicios.IniciarAsync(RutasServidor.ServicioApi, LimiteServicio, CancellationToken.None);
            return new(true, "El servidor volvió a la información anterior y está en marcha. La base recuperada se conserva.",
                ["Base anterior activada.", "Servidor en marcha."], sesion.Ruta);
        }
        catch (Exception error) when (error is FalloInstalacionException or OperationCanceledException or IOException
                                          or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new(false, error.Message + " " + await ReanudarAsync(activada: false), [], null);
        }
    }

    // Tras un fallo el servicio se vuelve a iniciar con la base que quedó activa: la anterior si no se llegó a activar la nueva.
    private async Task<string> ReanudarAsync(bool activada)
    {
        try
        {
            await sistema.Servicios.IniciarAsync(RutasServidor.ServicioApi, LimiteServicio, CancellationToken.None);
            return activada ? "El servidor volvió a iniciarse con la base recuperada." : "El servidor volvió a iniciarse con la base de siempre.";
        }
        catch (Exception error) when (error is FalloInstalacionException or System.ComponentModel.Win32Exception or InvalidOperationException
                                          or TimeoutException)
        {
            return "El servidor no pudo volver a iniciarse; revisa el servicio «" + NombreSistema.Visible + " - Servidor» en Windows.";
        }
    }

    private async Task<string> MantenimientoAsync(ContextoEjecucion contexto, string comando, string? entrada, CancellationToken ct,
        params string[] opciones)
    {
        var solicitud = ConfiguracionEfectiva.Mantenimiento(contexto, comando, LimiteMantenimiento, opciones) with { EntradaSecreta = entrada };
        var resultado = await sistema.Procesos.EjecutarAsync(solicitud, ct);
        var salida = contexto.Filtro.Limpiar(resultado.Salida);
        if (resultado.VencioLimite) throw new FalloInstalacionException("La herramienta de mantenimiento tardó demasiado y se detuvo.");
        if (resultado.Codigo != 0)
            throw new FalloInstalacionException(salida.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Length > 0)
                ?? "La herramienta de mantenimiento no terminó correctamente.");
        return salida;
    }

    private static string[] Opciones(SolicitudRecuperacion solicitud) => solicitud.ClaveRecuperacion is { Length: > 0 } clave
        ? ["--archivo", solicitud.Copia, "--clave-recuperacion", clave]
        : ["--archivo", solicitud.Copia];

    private static string Entrada(CredencialOperador credencial) => credencial.Usuario + "\n" + credencial.ClaveTexto() + "\n";

    [GeneratedRegex(@"Recuperación ([0-9a-fA-F-]{36}) pendiente de activar")]
    private static partial Regex IdRecuperacion();
}
