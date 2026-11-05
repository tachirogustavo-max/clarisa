using System.ComponentModel;
using System.Windows;
using Clarisa.Instalacion.Contratos;
using Clarisa.Setup.Modelos;
using Clarisa.Setup.Vistas;
using Clarisa.Setup.Servicios;

namespace Clarisa.Setup;

public partial class VentanaRetirada : Window
{
    private readonly SolicitudRetirada? solicitud;
    public VentanaRetirada() => InitializeComponent();
    public VentanaRetirada(ModeloRetirada modelo, SolicitudRetirada? solicitud) : this()
    {
        DataContext = modelo;
        this.solicitud = solicitud;
    }
    private ModeloRetirada? Modelo => DataContext as ModeloRetirada;
    private async void Iniciar(object sender, RoutedEventArgs e)
    {
        if (solicitud is not null && Modelo is { } modelo) await modelo.RevisarAsync(solicitud);
    }
    private async void Retirar(object sender, RoutedEventArgs e)
    {
        if (Modelo is not { PuedeRetirar: true } modelo) return;
        var confirmacion = new DialogoConfirmacion("Se retirarán los programas comprobados de este equipo. Los datos académicos, respaldos, claves y registros se conservarán. ¿Quieres continuar?")
        { Owner = this, Title = "Confirmar retirada" };
        if (confirmacion.ShowDialog() == true) await modelo.RetirarAsync(true);
    }
    private void Detener(object sender, RoutedEventArgs e) => Modelo?.SolicitarCancelacion();
    private void Cerrar(object sender, RoutedEventArgs e) => Close();
    private void CerrarVentana(object? sender, CancelEventArgs e)
    {
        if (Modelo is not { Ocupado: true } modelo) return;
        e.Cancel = true;
        if (!modelo.PuedeDetener) return;
        var confirmacion = new DialogoConfirmacion("La ventana permanecerá abierta hasta que el paso actual termine de forma segura. ¿Solicitar que se detenga después?")
        { Owner = this, Title = "Detener retirada" };
        if (confirmacion.ShowDialog() == true) modelo.SolicitarCancelacion();
    }
    private void AbrirRegistro(object sender, RoutedEventArgs e)
    {
        RegistroVisible.Abrir(this, Modelo?.ArchivoRegistro);
    }
}
