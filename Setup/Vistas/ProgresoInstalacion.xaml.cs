using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using Clarisa.Setup.Modelos;

namespace Clarisa.Setup.Vistas;

public partial class ProgresoInstalacion : UserControl
{
    public ProgresoInstalacion() => InitializeComponent();
    private void SeguirPaso(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is { } paso)
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => EnfocarPaso(paso)));
    }
    private void AjustarTamano(object sender, SizeChangedEventArgs e) => AjustarLista();
    private void CambiarDetalle(object sender, RoutedEventArgs e) => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(AjustarLista));
    private void AjustarLista()
    {
        if (Lista is null || Content is not Grid contenedor) return;
        var disponible = contenedor.RowDefinitions[2].ActualHeight;
        if (disponible <= 0) return;
        Lista.MaxHeight = DataContext is IModeloProgreso { MostrarDetalles: true } or IModeloProgreso { Finalizado: true } ? disponible
            : Math.Min(disponible, Math.Max(1, Math.Floor((disponible - 16) / 78)) * 78 + 16);
        if (Lista.SelectedItem is { } paso) EnfocarPaso(paso);
    }
    private void EnfocarPaso(object paso)
    {
        if (DataContext is IModeloProgreso { MostrarDetalles: true } or IModeloProgreso { Finalizado: true }) { Lista.ScrollIntoView(paso); return; }
        var visibles = Math.Max(1, (int)Math.Floor((Lista.ActualHeight - 16) / 78));
        BuscarDesplazamiento(Lista)?.ScrollToVerticalOffset(Math.Max(0, Lista.Items.IndexOf(paso) - visibles + 1));
    }
    private void CopiarDatos(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ModeloInstalacion { Estaciones: { } datos }) return;
        try
        {
            Clipboard.SetText($"Dirección del servidor: {datos.Direccion}\r\nCertificado: {datos.Certificado}\r\nHuella de verificación: {datos.Huella}\r\n");
            Copiado.Text = "Copiado. Pégalo en un documento o en el bloc de notas.";
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            Copiado.Text = "No se pudo copiar. Selecciona cada dato y cópialo con Ctrl + C.";
        }
    }

    private void AbrirCarpeta(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ModeloInstalacion { Estaciones: { } datos } || System.IO.Path.GetDirectoryName(datos.Certificado) is not { } carpeta) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{datos.Certificado}\"") { UseShellExecute = false }); }
        catch (System.ComponentModel.Win32Exception) { Copiado.Text = "No se pudo abrir la carpeta. Está en: " + carpeta; }
    }

    private static ScrollViewer? BuscarDesplazamiento(DependencyObject objeto)
    {
        if (objeto is ScrollViewer desplazamiento) return desplazamiento;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(objeto); i++)
            if (BuscarDesplazamiento(VisualTreeHelper.GetChild(objeto, i)) is { } encontrado) return encontrado;
        return null;
    }
}
