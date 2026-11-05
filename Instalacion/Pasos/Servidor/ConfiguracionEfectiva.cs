using System.Security;
using System.Text.Json.Nodes;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Pasos.Servidor;

// Configuración que describe la instalación anterior, fijada una sola vez por ejecución: la copia previa, la migración y el entorno
// nuevo deben partir de la misma base. Entorno es null cuando el mantenimiento debe leer servidor.json en lugar de las variables.
internal sealed record FuenteConfiguracion(JsonObject? Configuracion, IReadOnlyDictionary<string, string>? Entorno, string Descripcion);

// Problema es el motivo por el que unas variables existentes no son utilizables; nunca contiene valores.
internal sealed record LecturaEntorno(IReadOnlyDictionary<string, string> Valores, JsonObject? Configuracion, string? Problema)
{
    public static readonly LecturaEntorno Vacia = new(new Dictionary<string, string>(), null, null);
}

internal static class ConfiguracionEfectiva
{
    public static JsonObject? Leer(ContextoEjecucion contexto)
    {
        var fuente = Resolver(contexto);
        var archivo = contexto.RutasServidor.ArchivoConfiguracion;
        return File.Exists(archivo) ? ConfiguracionServidor.Leer(archivo) : fuente.Configuracion?.DeepClone() as JsonObject;
    }

    // Sin interacción: un entorno inutilizable o distinto de servidor.json solo lo decide ReparacionEntorno, antes de detener la API.
    public static FuenteConfiguracion Resolver(ContextoEjecucion contexto)
    {
        if (contexto.Fuente is { } decidida) return decidida;
        var archivo = ConfiguracionServidor.Leer(contexto.RutasServidor.ArchivoConfiguracion);
        var entorno = LeerEntorno(contexto);
        if (Conflicto(archivo, entorno) is { } conflicto)
            throw new FalloInstalacionException(conflicto + " Ejecuta el instalador completo para decidir la reparación antes de cambiar nada.");
        return contexto.Fuente = entorno.Configuracion is not null
            ? new FuenteConfiguracion(archivo ?? entorno.Configuracion, entorno.Valores, "entorno protegido del servicio")
            : new FuenteConfiguracion(archivo, null, archivo is null ? "sin configuración anterior" : "servidor.json");
    }

    public static string? Conflicto(JsonObject? archivo, LecturaEntorno entorno)
    {
        if (entorno.Problema is not null) return "El entorno protegido del servicio no es utilizable: " + entorno.Problema + ".";
        return entorno.Configuracion is not null && archivo is not null && !ConfiguracionServidor.MismoContenido(archivo, entorno.Configuracion)
            ? "servidor.json y el entorno protegido del servicio describen configuraciones distintas."
            : null;
    }

    public static LecturaEntorno LeerEntorno(ContextoEjecucion contexto)
    {
        if (ServiciosPropios.ConsultarPropio(contexto, RutasServidor.ServicioApi) is null) return LecturaEntorno.Vacia;
        EntornoServicio entorno;
        try
        {
            entorno = contexto.Sistema.Servicios.LeerEntorno(RutasServidor.ServicioApi);
        }
        catch (Exception error) when (error is InvalidDataException or UnauthorizedAccessException or IOException or SecurityException)
        {
            return LecturaEntorno.Vacia with { Problema = "el Registro del servicio no se pudo leer o tiene un formato no admitido" };
        }
        if (entorno.Valores.Count == 0) return LecturaEntorno.Vacia;
        if (!entorno.Protegido)
            return new(entorno.Valores, null, "la clave del servicio no tiene los permisos previstos y no se confía en sus valores");
        try
        {
            return new(entorno.Valores, EntornoServidor.Leer(entorno.Valores), null);
        }
        catch (FalloInstalacionException)
        {
            return new(entorno.Valores, null, "sus variables cifradas no se pueden verificar en este equipo");
        }
    }

    public static SolicitudProceso Mantenimiento(ContextoEjecucion contexto, string comando, TimeSpan limite, params string[] opciones)
    {
        var entorno = Resolver(contexto).Entorno;
        IReadOnlyList<string> fuente = entorno is not null ? ["--entorno"] : ["--configuracion", contexto.RutasServidor.ArchivoConfiguracion];
        return new(contexto.RutasServidor.EjecutableMantenimiento, [comando, .. fuente, .. opciones], limite) { VariablesEntorno = entorno };
    }

    // Solo identifica si el puerto ocupado es el del servicio propio; ante un entorno dañado basta la dirección de servidor.json.
    public static string? Url(ContextoEjecucion contexto)
    {
        try
        {
            return ConfiguracionServidor.UrlConfigurada(Leer(contexto));
        }
        catch (FalloInstalacionException)
        {
            return ConfiguracionServidor.UrlConfigurada(contexto.RutasServidor);
        }
    }
}
