namespace Clarisa.Application.Common.Errores;

public sealed class ErrorAplicacionException(TipoError tipo, string mensaje, string? campo = null, Exception? causa = null)
    : Exception(mensaje, causa)
{
    public TipoError Tipo { get; } = tipo;
    public string? Campo { get; } = campo;
}
