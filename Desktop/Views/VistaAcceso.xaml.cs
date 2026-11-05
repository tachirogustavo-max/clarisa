using System.Windows;
using System.Windows.Controls;
using Clarisa.Desktop.Presentation.Models;

namespace Clarisa.Desktop.Views;
public partial class VistaAcceso : UserControl
{
    public VistaAcceso() => InitializeComponent();
    private void MostrarAyudaCuenta(object sender, RoutedEventArgs e)
    {
        if (sender is not Expander ayuda || e.OriginalSource != ayuda) return;
        ayuda.UpdateLayout();
        ayuda.BringIntoView();
    }
    private async void IniciarAcceso(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ModeloAcceso modelo)
            return;
        using var clave = CampoClave.ObtenerClave();
        try
        {
            await modelo.IniciarSesionAsync(clave);
            if (modelo.ErrorServidor.Length > 0)
                Conexion.IsExpanded = true;
        }
        finally
        {
            // Una validación local no envía nada: se conserva la clave para corregir solo el dato señalado.
            if (modelo.UltimoIntentoEnviado)
                CampoClave.Limpiar();
        }
    }
}
