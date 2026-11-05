using System.Text;
using Clarisa.Domain.Exceptions;

namespace Clarisa.Domain.Common;

public static class Textos
{
    public static string Requerido(string? valor, string campo, int maximo)
    {
        var limpio = Limpiar(valor, campo);
        if (limpio.Length == 0 || limpio.Length > maximo)
            throw new ReglaDominioException(campo, $"{campo}: escribe entre 1 y {maximo} caracteres.");
        return limpio;
    }

    public static string? Opcional(string? valor, string campo, int maximo)
    {
        var limpio = Limpiar(valor, campo);
        if (limpio.Length > maximo)
            throw new ReglaDominioException(campo, $"{campo}: escribe como máximo {maximo} caracteres.");
        return limpio.Length == 0 ? null : limpio;
    }

    public static string Clave(string valor) => valor.ToUpperInvariant();

    public static bool Iguales(string? a, string? b) =>
        a is null || b is null ? a == b : string.Equals(Clave(a), Clave(b), StringComparison.Ordinal);

    private static string Limpiar(string? valor, string campo)
    {
        if (valor is null) return "";
        var resultado = new StringBuilder(valor.Length);
        var espacioPendiente = false;
        foreach (var caracter in valor.Trim())
        {
            if (char.IsWhiteSpace(caracter))
            {
                espacioPendiente = true;
                continue;
            }
            if (char.IsControl(caracter) || char.GetUnicodeCategory(caracter) == System.Globalization.UnicodeCategory.Format)
                throw new ReglaDominioException(campo, $"{campo} contiene caracteres no válidos.");
            if (espacioPendiente) resultado.Append(' ');
            espacioPendiente = false;
            resultado.Append(caracter);
        }
        return resultado.ToString();
    }
}
