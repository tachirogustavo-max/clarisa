using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Clarisa.Desktop.Controls;

namespace Clarisa.Desktop.Presentation.Behaviors;
public static class FocoErrorFormulario
{
    public static void Mostrar(FrameworkElement raiz, string? campo)
    {
        if (campo is null || Window.GetWindow(raiz)?.IsActive != true)
            return;
        var control = Encontrar(raiz, "Formulario." + (campo == "FechaNacimiento" ? "TextoFechaNacimiento" : campo));
        control?.BringIntoView();
        control?.Focus();
    }

    private static FrameworkElement? Encontrar(DependencyObject padre, string ruta)
    {
        var propiedad = padre switch
        {
            CampoFechaEditable => CampoFechaEditable.TextoProperty,
            DatePicker => DatePicker.SelectedDateProperty,
            ComboBox => ComboBox.SelectedItemProperty,
            TextBox => TextBox.TextProperty,
            CheckBox => CheckBox.IsCheckedProperty,
            _ => null
        };
        if (propiedad is not null && BindingOperations.GetBindingExpression(padre, propiedad)?.ParentBinding.Path?.Path == ruta && padre is FrameworkElement { IsVisible: true, IsEnabled: true } control)
            return control;
        for (var indice = 0; indice < VisualTreeHelper.GetChildrenCount(padre); indice++)
            if (Encontrar(VisualTreeHelper.GetChild(padre, indice), ruta)is { } encontrado)
                return encontrado;
        return null;
    }
}
