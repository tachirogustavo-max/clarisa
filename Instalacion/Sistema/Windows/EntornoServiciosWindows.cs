using System.Security.AccessControl;
using System.Security.Principal;
using Clarisa.Instalacion.Pasos.Servidor;
using Microsoft.Win32;

namespace Clarisa.Instalacion.Sistema.Windows;

internal static class EntornoServiciosWindows
{
    private const string Servicio = "Clarisa.Estudios";
    public static EntornoServicio Leer(string nombre)
    {
        ExigirNombre(nombre);
        using var equipo = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var clave = equipo.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + nombre, RegistryKeyPermissionCheck.ReadSubTree, RegistryRights.ReadKey);
        if (clave is null) return new(new Dictionary<string, string>(), false);
        var contenido = clave.GetValue("Environment", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (contenido is null) return new(new Dictionary<string, string>(), false);
        if (clave.GetValueKind("Environment") != RegistryValueKind.MultiString || contenido is not string[] lineas || lineas.Length != 2)
            throw new InvalidDataException("El entorno del servicio tiene un formato no admitido.");
        var valores = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var linea in lineas)
        {
            var separador = linea.IndexOf('=');
            if (separador < 1 || linea.Length > 24_100 || !valores.TryAdd(linea[..separador], linea[(separador + 1)..]))
                throw new InvalidDataException("El entorno del servicio tiene variables inválidas o repetidas.");
        }
        ExigirVariables(valores);
        return new(valores, Protegida(clave.GetAccessControl()));
    }
    public static void Guardar(string nombre, IReadOnlyDictionary<string, string> valores)
    {
        ExigirNombre(nombre);
        ExigirVariables(valores);
        using var equipo = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var clave = equipo.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + nombre,
            RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.FullControl)
            ?? throw new InvalidOperationException("El servicio debe existir antes de configurar su entorno.");
        clave.SetAccessControl(SeguridadEsperada());
        if (!Protegida(clave.GetAccessControl())) throw new UnauthorizedAccessException("No se pudo verificar la protección del entorno del servicio.");
        clave.SetValue("Environment", valores.Select(p => p.Key + "=" + p.Value).ToArray(), RegistryValueKind.MultiString);
        clave.Flush();
    }
    internal static RegistrySecurity SeguridadEsperada()
    {
        var seguridad = new RegistrySecurity();
        seguridad.SetAccessRuleProtection(true, false);
        seguridad.SetOwner(new SecurityIdentifier(IdentidadServicio.Administradores));
        foreach (var regla in Reglas()) seguridad.AddAccessRule(regla);
        return seguridad;
    }
    internal static bool Protegida(RegistrySecurity seguridad)
    {
        if (!seguridad.AreAccessRulesProtected || seguridad.GetOwner(typeof(SecurityIdentifier))?.Value != IdentidadServicio.Administradores) return false;
        var actuales = seguridad.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<RegistryAccessRule>().ToArray();
        var esperadas = Reglas();
        return actuales.Length == esperadas.Count && actuales.All(a => !a.IsInherited && a.AccessControlType == AccessControlType.Allow
            && esperadas.Any(e => e.IdentityReference.Equals(a.IdentityReference) && e.RegistryRights == a.RegistryRights
                && e.InheritanceFlags == a.InheritanceFlags && e.PropagationFlags == a.PropagationFlags));
    }
    // Heredables por subclaves: SetAccessControl propaga a las hijas, y una subclave del SCM que solo tenía reglas heredadas
    // (Security, TriggerInfo) quedaría sin ninguna, incluso para SYSTEM. Reproducido con una clave análoga en HKCU.
    private static IReadOnlyList<RegistryAccessRule> Reglas() =>
    [
        new(new SecurityIdentifier(IdentidadServicio.Administradores), RegistryRights.FullControl, InheritanceFlags.ContainerInherit,
            PropagationFlags.None, AccessControlType.Allow),
        new(new SecurityIdentifier(IdentidadServicio.Sistema), RegistryRights.FullControl, InheritanceFlags.ContainerInherit,
            PropagationFlags.None, AccessControlType.Allow),
        new(new SecurityIdentifier(IdentidadServicio.Sid(Servicio)), RegistryRights.ReadKey, InheritanceFlags.ContainerInherit,
            PropagationFlags.None, AccessControlType.Allow)
    ];
    private static void ExigirNombre(string nombre)
    {
        if (nombre != Servicio) throw new ArgumentException("Solo se configura el entorno del servicio propio.");
    }
    private static void ExigirVariables(IReadOnlyDictionary<string, string> valores)
    {
        if (valores.Count != 2 || !valores.ContainsKey(EntornoServidor.Ajustes) || !valores.ContainsKey(EntornoServidor.Conexion)
            || valores.Any(p => string.IsNullOrEmpty(p.Value) || p.Value.Length > 24_000 || p.Value.Any(char.IsControl))
            || valores.Sum(p => p.Key.Length + p.Value.Length + 2) > 30_000)
            throw new InvalidDataException("Las variables del servicio están incompletas o no son válidas.");
    }
}
