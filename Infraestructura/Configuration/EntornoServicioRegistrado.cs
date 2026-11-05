using Microsoft.Win32;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;

namespace Clarisa.Infrastructure.Configuration;

public static class EntornoServicioRegistrado
{
    public static ArchivoConfiguracionServidor Leer()
    {
        if (!OperatingSystem.IsWindows()) throw new ConfiguracionServidorException("El entorno del servicio requiere Windows.");
        try
        {
            using var equipo = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var servicio = equipo.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Clarisa.Estudios", false)
                ?? throw new ConfiguracionServidorException("No se encontró la configuración del servicio instalado.");
            ExigirProteccion(servicio.GetAccessControl());
            if (servicio.GetValue("Environment", null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not string[] lineas || lineas.Length != 2
                || servicio.GetValueKind("Environment") != RegistryValueKind.MultiString)
                throw new ConfiguracionServidorException("El entorno del servicio está incompleto.");
            var valores = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var linea in lineas)
            {
                var separador = linea.IndexOf('=');
                if (linea.Length > 24_100 || separador <= 0 || !valores.TryAdd(linea[..separador], linea[(separador + 1)..]))
                    throw new ConfiguracionServidorException("El entorno del servicio tiene variables inválidas o repetidas.");
            }
            if (!valores.ContainsKey(ConfiguracionEntornoServidor.Ajustes) || !valores.ContainsKey(ConfiguracionEntornoServidor.Conexion))
                throw new ConfiguracionServidorException("Faltan las variables propias del servidor.");
            return ConfiguracionEntornoServidor.Leer(nombre => valores.GetValueOrDefault(nombre));
        }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException or System.Security.SecurityException or IdentityNotMappedException)
        { throw new ConfiguracionServidorException("No se pudo leer el entorno protegido del servicio. Usa una consola de mantenimiento autorizada."); }
    }
    [SupportedOSPlatform("windows")]
    private static void ExigirProteccion(RegistrySecurity seguridad)
    {
        var servicio = (SecurityIdentifier)new NTAccount(@"NT SERVICE\Clarisa.Estudios").Translate(typeof(SecurityIdentifier));
        if (!ProteccionValida(seguridad, servicio))
            throw new ConfiguracionServidorException("El entorno registrado no tiene los permisos de protección previstos.");
    }

    // Separada de la traducción de la cuenta para comprobar en memoria que coincide con la ACL que escribe el instalador. Acepta las
    // reglas heredables por subclaves (las escribe el instalador actual) y las de solo la clave (instalaciones anteriores hasta su reparación).
    [SupportedOSPlatform("windows")]
    public static bool ProteccionValida(RegistrySecurity seguridad, SecurityIdentifier servicio)
    {
        ArgumentNullException.ThrowIfNull(seguridad);
        var administradores = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var sistema = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var reglas = seguridad.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<RegistryAccessRule>().ToArray();
        return seguridad.AreAccessRulesProtected && administradores.Equals(seguridad.GetOwner(typeof(SecurityIdentifier))) && reglas.Length == 3
            && reglas.Select(r => r.InheritanceFlags).Distinct().Count() == 1
            && !reglas.Any(r => r.IsInherited || r.AccessControlType != AccessControlType.Allow
                || r.InheritanceFlags is not (InheritanceFlags.None or InheritanceFlags.ContainerInherit)
                || r.PropagationFlags != PropagationFlags.None || !((r.IdentityReference.Equals(administradores) || r.IdentityReference.Equals(sistema))
                    && r.RegistryRights == RegistryRights.FullControl || r.IdentityReference.Equals(servicio) && r.RegistryRights == RegistryRights.ReadKey))
            && reglas.Select(r => r.IdentityReference.Value).Distinct(StringComparer.Ordinal).Count() == 3;
    }
}
