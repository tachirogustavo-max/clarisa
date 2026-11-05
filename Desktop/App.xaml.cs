using System.Windows;
using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services;
using Clarisa.Desktop.Configuration;
using Clarisa.Desktop.Views;
using System.ComponentModel;

namespace Clarisa.Desktop;
public partial class App : System.Windows.Application
{
    private ClienteSesiones? cliente;
    private readonly CancellationTokenSource cierre = new();
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        cliente = new ClienteSesiones();
        var configuracion = ConfiguracionEstacion.Leer(e.Args);
        var modelo = new ModeloAcceso(cliente, configuracion.Aviso) { Servidor = configuracion.Servidor };
        var ventana = new MainWindow
        {
            DataContext = modelo
        };
        MainWindow = ventana;
        ConectarAcceso(ventana, modelo);
        ventana.Show();
    }

    private void ConectarAcceso(MainWindow ventana, ModeloAcceso acceso)
    {
        void SesionCambiada(object? sender, PropertyChangedEventArgs cambio)
        {
            if (cambio.PropertyName != nameof(ModeloAcceso.Sesion) || acceso.Sesion is not { } sesion || cliente is null)
                return;
            acceso.PropertyChanged -= SesionCambiada;
            var servidor = acceso.ServidorSesion!;
            var fabrica = new FabricaModulos(cliente.Transporte, servidor, sesion.Perfil == "Administracion");
            var escritorio = new ModeloEscritorio(sesion, fabrica.Crear, new ClienteClavePropia(cliente.Transporte, servidor));
            fabrica.PlanillaLista += (origen, planilla) =>
            {
                if (!escritorio.CerrandoSesion && ReferenceEquals(escritorio.Contenido, origen))
                    escritorio.AbrirDetalle(ModuloVista.Calificaciones, planilla);
                else
                    planilla.Cancelar();
            };
            var vista = new VistaEscritorio
            {
                DataContext = escritorio
            };
            var conexionSesion = cliente;
            vista.CerrarSesion += async (_, _) => await CerrarSesionAsync(ventana, escritorio, servidor, conexionSesion);
            ventana.DataContext = escritorio;
            ventana.Content = vista;
        }

        acceso.PropertyChanged += SesionCambiada;
    }

    private async Task CerrarSesionAsync(MainWindow ventana, ModeloEscritorio escritorio, Uri servidor, ClienteSesiones conexion)
    {
        if (!escritorio.IniciarCierreSesion())
            return;
        ResultadoCierreSesion resultado;
        try
        {
            resultado = await conexion.CerrarAsync(servidor, cierre.Token);
        }
        catch (OperationCanceledException) when (cierre.IsCancellationRequested)
        {
            return;
        }
        finally
        {
            conexion.Dispose();
        }
        if (cierre.IsCancellationRequested || !ventana.IsLoaded || !ReferenceEquals(ventana.DataContext, escritorio))
            return;
        cliente = new ClienteSesiones();
        var nuevoAcceso = new ModeloAcceso(cliente, resultado.Aviso ?? escritorio.AvisoSalida) { Servidor = servidor.AbsoluteUri };
        ventana.DataContext = nuevoAcceso;
        ventana.Content = new VistaAcceso();
        ConectarAcceso(ventana, nuevoAcceso);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        cierre.Cancel();
        cliente?.Dispose();
        base.OnExit(e);
    }
}
