using Clarisa.Domain.Exceptions;

namespace Clarisa.Domain.Common;

public static class Identificaciones
{
    public const int Minimo = 3;
    public const int Maximo = 20;

    // El formato oficial del cliente no está confirmado: se admite cualquier código alfanumérico con guiones.
    public static string Normalizar(string? valor, string campo)
    {
        var limpio = (valor ?? "").Trim().Replace(" ", "").Replace(".", "").ToUpperInvariant();
        if (limpio.Length < Minimo || limpio.Length > Maximo || !limpio.All(EsPermitido)
            || limpio.StartsWith('-') || limpio.EndsWith('-') || limpio.Contains("--"))
            throw new ReglaDominioException(campo, $"{campo}: usa de {Minimo} a {Maximo} letras, números o guiones.");
        return limpio;
    }

    public static string? NormalizarOpcional(string? valor, string campo) =>
        string.IsNullOrWhiteSpace(valor) ? null : Normalizar(valor, campo);

    private static bool EsPermitido(char caracter) => caracter is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-';
}
