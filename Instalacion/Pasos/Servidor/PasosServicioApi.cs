using System.Net;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Pasos.Servidor;

// En actualización o reparación los servicios propios se detienen antes de sustituir binarios; los ajenos nunca se tocan.
internal sealed class PasoDetenerServicios : IPasoInstalacion
{
    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(60);
    private static readonly string[] Nombres = [RutasServidor.ServicioApi, RutasServidor.ServicioMotor];

    public string Id => "detener-servicios";
    public string Nombre => "Servicios propios detenidos para actualizar";
    public string Descripcion => "Si existe una instalación anterior, detiene sus servicios para sustituir programas sin archivos bloqueados.";
    public bool Critico => true;
    public bool Privilegiado => true;

    public Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var servicios = Nombres.Select(contexto.Sistema.Servicios.Consultar).ToList();
        if (servicios.Select(s => ServiciosPropios.Ajeno(contexto, s)).FirstOrDefault(a => a is not null) is { } ajeno)
            return Task.FromResult(Comprobacion.NoSatisfecha(ajeno));
        var activos = servicios.Where(s => s is not null && s.Estado != EstadoServicio.Detenido).Select(s => s!.Nombre).ToList();
        return Task.FromResult(activos.Count == 0
            ? Comprobacion.Satisfecha("No hay servicios propios en ejecución.")
            : Comprobacion.NoSatisfecha("En ejecución: " + string.Join(", ", activos)));
    }

    public async Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        foreach (var nombre in Nombres)
            if (ServiciosPropios.ConsultarPropio(contexto, nombre)?.Estado is { } estado && estado != EstadoServicio.Detenido)
                await contexto.Sistema.Servicios.DetenerAsync(nombre, Limite, ct);
    }
}

internal sealed class PasoServicioApi : IPasoInstalacion
{
    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(90);

    public string Id => "servicio-api";
    public string Nombre => "Servicio del servidor del liceo";
    public string Descripcion => "Registra la API con cuenta virtual y variables protegidas exclusivas del servicio; comprueba su configuración e inicia.";
    public bool Critico => true;
    public bool Privilegiado => true;

    public Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var servicio = contexto.Sistema.Servicios.Consultar(RutasServidor.ServicioApi);
        var diferencia = ServiciosPropios.Ajeno(contexto, servicio) ?? ComparadorServicio.Diferencia(servicio, Definicion(contexto.RutasServidor));
        if (diferencia is null)
        {
            var entorno = ConfiguracionEfectiva.LeerEntorno(contexto);
            if (entorno.Problema is not null) diferencia = "El entorno del servicio no es utilizable: " + entorno.Problema + ".";
            else if (entorno.Configuracion is null || !ConfiguracionServidor.MismoContenido(entorno.Configuracion, ConfiguracionEfectiva.Leer(contexto)))
                diferencia = "Las variables del servicio no coinciden con la configuración elegida.";
        }
        return Task.FromResult(diferencia is null
            ? Comprobacion.Satisfecha($"Servicio {RutasServidor.ServicioApi} en ejecución con {IdentidadServicio.Cuenta(RutasServidor.ServicioApi)}.")
            : Comprobacion.NoSatisfecha(diferencia));
    }

    public async Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var servicios = contexto.Sistema.Servicios;
        var existia = ServiciosPropios.ConsultarPropio(contexto, RutasServidor.ServicioApi) is not null;
        var configuracion = ConfiguracionEfectiva.Leer(contexto) ?? throw new FalloInstalacionException("Falta la configuración para preparar el entorno.");
        var entorno = EntornoServidor.Crear(configuracion);
        // Las variables van antes que la línea de comando: si su escritura falla, un servicio que leía servidor.json sigue arrancando.
        if (existia) servicios.GuardarEntorno(RutasServidor.ServicioApi, entorno);
        servicios.CrearOActualizar(Definicion(contexto.RutasServidor));
        contexto.Propio(TipoArtefacto.Servicio, RutasServidor.ServicioApi);
        if (!existia) servicios.GuardarEntorno(RutasServidor.ServicioApi, entorno);
        if (servicios.Consultar(RutasServidor.ServicioApi)?.Estado != EstadoServicio.EnEjecucion)
            await servicios.IniciarAsync(RutasServidor.ServicioApi, Limite, ct);
    }

    internal static DefinicionServicio Definicion(RutasServidor rutas) => new(RutasServidor.ServicioApi, NombreSistema.Visible + " - Servidor",
        "Servidor del liceo para las estaciones de la red local.", rutas.EjecutableApi,
        ["--entorno-servicio"], IdentidadServicio.Cuenta(RutasServidor.ServicioApi), [RutasServidor.ServicioMotor]);
}

// La regla se identifica por nombre, pero solo se modifica si esta instalación la creó o ya es del grupo propio para su programa.
internal sealed class PasoRedLocal : IPasoInstalacion
{
    public const string NombreRegla = "Clarisa Estudios - Servidor HTTPS";
    private const int PerfilesLocales = (int)(PerfilRed.Dominio | PerfilRed.Privado);

    public string Id => "red-local";
    public string Nombre => "Acceso desde la red local";
    public string Descripcion => "Permite la entrada TCP solo al programa del servidor, en su dirección y puerto, desde la subred local y redes privadas o de dominio.";
    public bool Critico => true;
    public bool Privilegiado => true;

    public Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var firewall = contexto.Sistema.Firewall;
        var esperada = Esperada(contexto);
        var reglas = firewall.Consultar(NombreRegla);
        if (Conflicto(contexto, reglas, esperada) is { } conflicto) return Task.FromResult(Comprobacion.NoSatisfecha(conflicto));
        if (reglas.Count == 0) return Task.FromResult(Comprobacion.NoSatisfecha("No existe la regla de entrada del servidor."));
        if (reglas[0].Diferencia(esperada) is { } diferencia)
            return Task.FromResult(Comprobacion.NoSatisfecha($"La regla de entrada existe con {diferencia}."));
        if ((firewall.PerfilesActivos() & PerfilesLocales) == 0)
            return Task.FromResult(Comprobacion.Indeterminada("La regla existe, pero la red activa de este equipo es Pública: las estaciones no " +
                                                             "podrán conectar. Marca la red del liceo como Privada en Windows y vuelve a comprobar."));
        return Task.FromResult(Comprobacion.Satisfecha(
            $"Regla «{NombreRegla}» para {esperada.DireccionLocal}:{esperada.Puerto}, solo desde la subred local. La conexión desde otra " +
            "estación se comprueba al instalarla."));
    }

    public Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var firewall = contexto.Sistema.Firewall;
        var esperada = Esperada(contexto);
        var reglas = firewall.Consultar(NombreRegla);
        if (Conflicto(contexto, reglas, esperada) is { } conflicto) throw new FalloInstalacionException(conflicto);
        if (reglas.Count == 1) firewall.Actualizar(esperada);
        else firewall.Crear(esperada);
        contexto.Propio(TipoArtefacto.ReglaFirewall, NombreRegla);
        return Task.CompletedTask;
    }

    private static string? Conflicto(ContextoEjecucion contexto, IReadOnlyList<ReglaFirewall> reglas, ReglaFirewall esperada)
    {
        if (reglas.Count > 1)
            return $"Hay {reglas.Count} reglas de firewall llamadas «{NombreRegla}»; no se decide cuál es la propia ni se borra ninguna.";
        if (reglas.Count == 0 || contexto.Estado.EsPropio(TipoArtefacto.ReglaFirewall, NombreRegla)) return null;
        var actual = reglas[0];
        return actual.Grupo == ReglaFirewall.GrupoPropio && ProgramasAnteriores.MismoArchivo(actual.Programa, esperada.Programa, contexto.RutasServidor.EjecutableApiAnterior)
            ? null
            : $"Ya existe una regla «{NombreRegla}» que no creó esta instalación; no se modifica. Revísala con el responsable técnico.";
    }

    private static ReglaFirewall Esperada(ContextoEjecucion contexto) =>
        new(NombreRegla, contexto.RutasServidor.EjecutableApi, contexto.Servidor.PuertoHttps, contexto.Servidor.DireccionEscucha);
}

internal sealed class PasoCopiasAutomaticas : IPasoInstalacion
{
    public string Id => "copias-automaticas";
    public string Nombre => "Copias automáticas";
    public string Descripcion => "El propio servidor hace una copia cifrada cada día; no se registra ninguna tarea de Windows.";
    public bool Critico => false;
    public bool Privilegiado => false;

    public Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct) => Task.FromResult(Comprobacion.Satisfecha(
        "El servidor hace una copia cifrada y verificada cada día desde las 13:00 (o al encenderse, si estaba apagado a esa hora) y " +
        "conserva las 30 automáticas más recientes; las copias manuales no se retiran. No se registró ninguna tarea de Windows."));

    public Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct) => Task.CompletedTask;
}

internal static class RespuestaEsperada
{
    public static bool ApiActiva(HttpStatusCode estado) => estado == HttpStatusCode.Unauthorized;
}
