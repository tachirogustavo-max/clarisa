using System.Windows.Controls;
using Clarisa.Desktop.Controls;
using Clarisa.Desktop.Presentation.Models;

namespace Clarisa.Desktop.Views;
public partial class VistaUsuarios : UserControl
{
    public VistaUsuarios() => InitializeComponent();
    private void ActualizarClave(object? sender, EventArgs e)
    {
        if (sender is CampoClaveSeguro campo && DataContext is ModeloUsuarios { Formulario: { } formulario })
            formulario.ClaveEditada = campo.TieneContenido;
    }
}
