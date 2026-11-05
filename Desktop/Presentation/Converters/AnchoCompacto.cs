using System.Globalization;
using System.Windows.Data;

namespace Clarisa.Desktop.Presentation.Converters;
public sealed class AnchoCompacto : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var limite = parameter is string texto && double.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var numero) ? numero : 920;
        return value is double ancho && ancho > 0 && ancho < limite;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
