namespace Clarisa.Domain.Exceptions;

public sealed class ReglaDominioException(string campo, string mensaje) : Exception(mensaje)
{
    public string Campo { get; } = campo;
}
