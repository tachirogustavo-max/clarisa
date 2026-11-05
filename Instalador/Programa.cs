using System.Windows;

namespace Clarisa.Instalador;

// Instalador de un solo archivo: lleva dentro la carpeta completa (Clarisa.Setup.exe, paquete y licencias), la descomprime en una
// carpeta temporal que solo pueden tocar los administradores, abre Clarisa.Setup.exe y borra la carpeta cuando este se cierra.
public static class Programa
{
    [STAThread]
    public static int Main(string[] argumentos)
    {
        var aplicacion = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var ventana = new VentanaPreparacion(argumentos);
        ventana.Show();
        return aplicacion.Run();
    }
}
