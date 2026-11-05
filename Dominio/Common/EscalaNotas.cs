using Clarisa.Domain.Exceptions;

namespace Clarisa.Domain.Common;

public static class EscalaNotas
{
    public const decimal Minima = 1m;
    public const decimal Maxima = 20m;

    // Capacidad de almacenamiento, no precisión académica: la política del liceo puede admitir menos decimales.
    public const int DecimalesTecnicosMaximos = 2;

    public static decimal Validar(decimal valor, int decimalesAdmitidos, string campo)
    {
        if (decimalesAdmitidos is < 0 or > DecimalesTecnicosMaximos)
            throw new ArgumentOutOfRangeException(nameof(decimalesAdmitidos));
        if (valor is < Minima or > Maxima)
            throw new ReglaDominioException(campo, $"La nota debe estar entre {Minima:0} y {Maxima:0}. Deje el campo vacío si está pendiente.");
        if (decimal.Round(valor, decimalesAdmitidos) != valor)
            throw new ReglaDominioException(campo, decimalesAdmitidos == 0
                ? "La nota debe ser un número entero."
                : $"La nota admite como máximo {decimalesAdmitidos} decimales.");
        return valor;
    }

    public static decimal? ValidarOpcional(decimal? valor, int decimalesAdmitidos, string campo) =>
        valor is { } nota ? Validar(nota, decimalesAdmitidos, campo) : null;
}
