using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace Clarisa.Instalador;

public partial class VentanaPreparacion : Window
{
    private readonly string[] argumentos;
    private readonly CancellationTokenSource cancelacion = new();
    private string? carpeta;
    private bool trabajando = true;

    public VentanaPreparacion(string[] argumentos)
    {
        this.argumentos = argumentos;
        InitializeComponent();
        Loaded += async (_, _) => await PrepararAsync();
        MouseLeftButtonDown += (_, _) => DragMove();
    }

    private async Task PrepararAsync()
    {
        int codigo;
        try
        {
            using var contenido = Contenido.Abrir()
                ?? throw new InvalidDataException("Este archivo no trae el paquete de instalación. Descarga de nuevo el instalador completo.");
            carpeta = Contenido.CrearCarpeta();
            var avance = new Progress<double>(valor => Avance.Value = valor);
            await Task.Run(() => Contenido.Descomprimir(contenido, carpeta, avance, cancelacion.Token));
            var setup = Path.Combine(carpeta, "Clarisa.Setup.exe");
            if (!File.Exists(setup)) throw new InvalidDataException("El instalador está incompleto: falta Clarisa.Setup.exe.");
            var inicio = new ProcessStartInfo(setup) { UseShellExecute = false, WorkingDirectory = carpeta };
            foreach (var argumento in argumentos) inicio.ArgumentList.Add(argumento);
            using var proceso = Process.Start(inicio) ?? throw new Win32Exception("No se pudo abrir el instalador.");
            Hide();
            await proceso.WaitForExitAsync();
            codigo = proceso.ExitCode;
        }
        catch (OperationCanceledException)
        {
            codigo = 1;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or Win32Exception)
        {
            Mostrar(error.Message);
            await Task.Run(() => { if (carpeta is not null) Contenido.Borrar(carpeta); });
            return;
        }
        Estado.Text = "Cerrando…";
        await Task.Run(() => { if (carpeta is not null) Contenido.Borrar(carpeta); });
        trabajando = false;
        Application.Current.Shutdown(codigo);
    }

    private void Mostrar(string mensaje)
    {
        trabajando = false;
        Show();
        Estado.Text = "No se pudo preparar la instalación. " + mensaje;
        Avance.Visibility = Visibility.Collapsed;
        Cerrar.Visibility = Visibility.Visible;
    }

    private void CerrarVentana(object sender, RoutedEventArgs e) => Application.Current.Shutdown(1);

    protected override void OnClosing(CancelEventArgs e)
    {
        if (trabajando) cancelacion.Cancel();
        base.OnClosing(e);
    }
}
