using Microsoft.Win32;

namespace Clarisa.Instalacion.Sistema.Windows;

// Vista de 64 bits de HKLM: la que muestra «Aplicaciones instaladas» para programas de equipo. Solo subclaves propias por nombre fijo.
internal sealed class RegistroAplicacionesWindows : IRegistroAplicaciones
{
    private const string Raiz = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public EntradaAplicacion? Leer(string clave)
    {
        ExigirClave(clave);
        using var equipo = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var entrada = equipo.OpenSubKey(Raiz + "\\" + clave);
        if (entrada is null) return null;
        string Texto(string nombre) => entrada.GetValue(nombre, "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
        return new EntradaAplicacion(clave, Texto("DisplayName"), Texto("DisplayVersion"), Texto("DisplayIcon"), Texto("InstallLocation"),
            Texto("UninstallString"), entrada.GetValue("EstimatedSize") is int tamano ? tamano : 0);
    }

    public void Escribir(EntradaAplicacion entrada)
    {
        ExigirClave(entrada.Clave);
        using var equipo = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var clave = equipo.CreateSubKey(Raiz + "\\" + entrada.Clave, writable: true)
                          ?? throw new UnauthorizedAccessException("No se pudo crear la entrada de Aplicaciones instaladas.");
        clave.SetValue("DisplayName", entrada.Nombre, RegistryValueKind.String);
        clave.SetValue("DisplayVersion", entrada.Version, RegistryValueKind.String);
        clave.SetValue("DisplayIcon", entrada.Icono, RegistryValueKind.String);
        clave.SetValue("InstallLocation", entrada.Ubicacion, RegistryValueKind.String);
        clave.SetValue("UninstallString", entrada.Desinstalar, RegistryValueKind.String);
        clave.SetValue("NoModify", 1, RegistryValueKind.DWord);
        clave.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        clave.SetValue("EstimatedSize", entrada.TamanoKb, RegistryValueKind.DWord);
        clave.Flush();
    }

    // Sin subclaves ni recursión: la entrada propia solo tiene valores.
    public void Eliminar(string clave)
    {
        ExigirClave(clave);
        using var equipo = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        equipo.DeleteSubKey(Raiz + "\\" + clave, throwOnMissingSubKey: false);
    }

    private static void ExigirClave(string clave)
    {
        if (clave is not (RegistroRetirada.ClaveServidor or RegistroRetirada.ClaveEstacion))
            throw new ArgumentException("Solo se gestionan las entradas propias de Aplicaciones instaladas.");
    }
}
