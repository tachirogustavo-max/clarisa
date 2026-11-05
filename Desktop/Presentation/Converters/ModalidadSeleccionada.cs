using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Clarisa.Desktop.Presentation.Converters;
public sealed class ModalidadSeleccionada : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int modalidad && int.TryParse(parameter?.ToString(), out var seleccion) && modalidad == seleccion;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true && int.TryParse(parameter?.ToString(), out var seleccion) ? seleccion : Binding.DoNothing;
}
