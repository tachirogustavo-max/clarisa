using Clarisa.Domain.Exceptions;

namespace Clarisa.Domain.Common;

public static class Correos
{
    public const int Maximo = 254;

    public static string? NormalizarOpcional(string? valor, string campo)
    {
        var correo = (valor ?? "").Trim().ToLowerInvariant();
        if (correo.Length == 0) return null;
        var arroba = correo.IndexOf('@');
        var valido = correo.Length <= Maximo
            && arroba > 0
            && arroba == correo.LastIndexOf('@')
            && correo.IndexOf('.', arroba) > arroba + 1
            && !correo.EndsWith('.')
            && !correo.Any(c => char.IsWhiteSpace(c) || char.IsControl(c));
        if (!valido) throw new ReglaDominioException(campo, $"{campo} no tiene un formato válido.");
        return correo;
    }
}
