namespace Clarisa.Application.Abstractions.Seguridad;

public sealed record TokenSesion(string Valor, string Huella)
{
    public override string ToString() => "Token de sesión protegido";
}

public interface IGeneradorTokensSesion
{
    TokenSesion Generar();

    // Null si el valor no tiene el formato de un token emitido: una cookie manipulada no llega a la base.
    string? Huella(string? valor);
}
