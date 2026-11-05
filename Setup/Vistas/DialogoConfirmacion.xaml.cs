using System.Windows;

namespace Clarisa.Setup.Vistas;

public partial class DialogoConfirmacion : Window
{
    public DialogoConfirmacion() => InitializeComponent();
    public DialogoConfirmacion(string mensaje) : this() => Mensaje.Text = mensaje;
    private void Confirmar(object sender, RoutedEventArgs e) => DialogResult = true;
}
