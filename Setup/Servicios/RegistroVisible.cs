using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Clarisa.Setup.Vistas;

namespace Clarisa.Setup.Servicios;

internal static class RegistroVisible
{
    public static void Abrir(Window propietario, string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta)) return;
        try
        {
            if (!File.Exists(ruta)) throw new FileNotFoundException();
            Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            new DialogoAviso("Registro de la operación", "No se pudo abrir el registro. Revisa que el archivo siga disponible y que tengas permiso para leerlo. Ubicación: " + ruta)
            { Owner = propietario }.ShowDialog();
        }
    }
}
