using System.Text;
using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Application.Common.Errores;

namespace Clarisa.Application.Features.Cuentas;

// La clave conserva sus espacios y se normaliza a NFC antes de calcular o verificar la huella.
public static class PoliticaClaves
{
    public const int Minimo = 15;
    public const int Maximo = 256;

    // Límite de lo que se acepta al iniciar sesión antes de calcular nada, para no gastar CPU con entradas enormes.
    public const int MaximoEntrada = 1024;

    public static string ValidarNueva(string? clave, string usuarioNormalizado, IListaClavesProhibidas prohibidas)
    {
        ArgumentNullException.ThrowIfNull(prohibidas);
        var normalizada = ValidarForma(clave, usuarioNormalizado);
        if (prohibidas.Contiene(normalizada))
            throw Error.DatosInvalidos("Esa contraseña es de uso común o aparece en filtraciones conocidas. Elige otra.", "Clave");
        return normalizada;
    }

    private static string ValidarForma(string? clave, string usuarioNormalizado)
    {
        var normalizada = NormalizarEntrada(clave);
        var caracteres = normalizada?.EnumerateRunes().Count() ?? 0;
        if (normalizada is null || caracteres is < Minimo or > Maximo)
            throw Error.DatosInvalidos($"La contraseña debe tener entre {Minimo} y {Maximo} caracteres válidos.", "Clave");
        if (string.IsNullOrWhiteSpace(normalizada) || normalizada.Any(char.IsControl))
            throw Error.DatosInvalidos("La contraseña contiene caracteres no válidos o solo espacios.", "Clave");
        if (string.Equals(normalizada.Trim(), usuarioNormalizado, StringComparison.OrdinalIgnoreCase))
            throw Error.DatosInvalidos("La contraseña no puede ser igual al usuario.", "Clave");
        return normalizada;
    }

    // Null si la entrada está vacía, es demasiado larga o tiene un sustituto UTF-16 aislado que no se puede normalizar.
    public static string? NormalizarEntrada(string? clave)
    {
        if (string.IsNullOrEmpty(clave) || clave.Length > MaximoEntrada) return null;
        try
        {
            return clave.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
