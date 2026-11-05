#if DEBUG
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Clarisa.Setup;

internal static class ComprobacionMarco
{
    public static void Ejecutar(Window ventana, string ruta)
    {
        var resultados = new List<string>();
        void Comprobar(bool cumple, string nombre)
        {
            resultados.Add((cumple ? "APROBADO " : "FALLO ") + nombre);
            File.WriteAllLines(Path.ChangeExtension(ruta, ".comandos.log"), resultados);
            if (!cumple) throw new InvalidOperationException(nombre);
        }
        void Asentar()
        {
            ventana.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(ventana.UpdateLayout));
            var cuadro = new DispatcherFrame();
            var espera = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            espera.Tick += (_, _) => { espera.Stop(); cuadro.Continue = false; };
            espera.Start();
            Dispatcher.PushFrame(cuadro);
        }
        if (ventana.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip)
        {
            SystemCommands.MaximizeWindowCommand.Execute(null, ventana);
            Asentar();
            Comprobar(ventana.WindowState == WindowState.Maximized, "Maximizar Setup");
            SystemCommands.MaximizeWindowCommand.Execute(null, ventana);
            Asentar();
            Comprobar(ventana.WindowState == WindowState.Normal, "Restaurar Setup");
            SystemCommands.MinimizeWindowCommand.Execute(null, ventana);
            Asentar();
            Comprobar(ventana.WindowState == WindowState.Minimized, "Minimizar Setup");
            SystemCommands.RestoreWindow(ventana);
            Asentar();
        }
        else
        {
            Comprobar(!SystemCommands.MaximizeWindowCommand.CanExecute(null, ventana), "Diálogo de credenciales no permite maximizar");
            Comprobar(!SystemCommands.MinimizeWindowCommand.CanExecute(null, ventana), "Diálogo de credenciales no permite minimizar");
        }
        var solicitoCerrar = false;
        System.ComponentModel.CancelEventHandler impedir = (_, e) => { solicitoCerrar = true; e.Cancel = true; };
        ventana.Closing += impedir;
        SystemCommands.CloseWindowCommand.Execute(null, ventana);
        Asentar();
        Comprobar(solicitoCerrar && ventana.IsVisible, "El comando respeta Closing cancelado");
        ventana.Closing -= impedir;
        SystemCommands.CloseWindowCommand.Execute(null, ventana);
        Asentar();
        Comprobar(!ventana.IsVisible, "El comando cierra cuando se autoriza");
    }
}
#endif
