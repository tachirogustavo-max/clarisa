using Clarisa.Domain.Exceptions;

namespace Clarisa.Domain.Common;

// Solo ASCII en minúsculas: evita que dos nombres visualmente iguales con letras de otro alfabeto sean cuentas distintas.
public static class NombresUsuario
{
    public const int Minimo = 3;
    public const int Maximo = 40;

    public static string Normalizar(string? valor, string campo)
    {
        if (!IntentarNormalizar(valor, out var normalizado))
            throw new ReglaDominioException(campo, $"{campo}: usa de {Minimo} a {Maximo} letras sin acento, números, punto, guion o guion bajo.");
        return normalizado;
    }

    public static bool IntentarNormalizar(string? valor, out string normalizado)
    {
        var limpio = (valor ?? "").Trim();
        // Se valida antes de pasar a minúsculas: algunos caracteres no ASCII (signo Kelvin, İ) se convierten en letras ASCII.
        var valido = limpio.Length is >= Minimo and <= Maximo && limpio.All(EsPermitido);
        normalizado = valido ? limpio.ToLowerInvariant() : "";
        return valido;
    }

    private static bool EsPermitido(char caracter) =>
        caracter is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '.' or '_' or '-';
}
