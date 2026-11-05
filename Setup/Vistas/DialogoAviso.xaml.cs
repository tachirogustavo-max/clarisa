using System.Windows;

namespace Clarisa.Setup.Vistas;

public partial class DialogoAviso : Window
{
    public DialogoAviso() => InitializeComponent();
    public DialogoAviso(string titulo, string mensaje) : this()
    {
        Title = titulo;
        Mensaje.Text = mensaje;
    }
}
