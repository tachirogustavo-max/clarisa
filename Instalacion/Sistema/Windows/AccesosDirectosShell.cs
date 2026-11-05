using System.Runtime.InteropServices;

namespace Clarisa.Instalacion.Sistema.Windows;

// IShellLink mediante el objeto COM de Windows Script Host; se relee el archivo para comprobar destino y argumentos.
internal sealed class AccesosDirectosShell : IAccesosDirectos
{
    public void Crear(string archivo, string destino, string argumentos, string directorioTrabajo, string descripcion)
    {
        dynamic shell = Shell();
        try
        {
            dynamic acceso = shell.CreateShortcut(archivo);
            try
            {
                acceso.TargetPath = destino;
                acceso.Arguments = argumentos;
                acceso.WorkingDirectory = directorioTrabajo;
                acceso.Description = descripcion;
                acceso.IconLocation = destino + ",0";
                acceso.Save();
            }
            finally
            {
                Marshal.FinalReleaseComObject(acceso);
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }
    }

    public (string Destino, string Argumentos)? Leer(string archivo)
    {
        if (!File.Exists(archivo)) return null;
        dynamic shell = Shell();
        try
        {
            dynamic acceso = shell.CreateShortcut(archivo);
            try
            {
                return ((string)acceso.TargetPath, (string)acceso.Arguments);
            }
            finally
            {
                Marshal.FinalReleaseComObject(acceso);
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }
    }

    private static object Shell() => Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")
                                                              ?? throw new Ejecucion.FalloInstalacionException("Windows Script Host no está disponible."))!;
}
