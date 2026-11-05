using System.Globalization;
using System.Windows.Data;

namespace Clarisa.Desktop.Presentation.Converters;
public sealed class EspacioCompacto : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) => values.Length == 2 && values[0] is double ancho && values[1] is double alto && ancho > 0 && alto > 0 && (ancho < 920 || alto < 720);
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException("La distribución se calcula a partir del espacio disponible.");
}
