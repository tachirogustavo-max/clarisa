using Clarisa.Domain.Exceptions;

namespace Clarisa.Domain.Common;

public static class Instantes
{
    public static DateTimeOffset Utc(DateTimeOffset valor) => valor.ToUniversalTime();

    public static DateTimeOffset ExigirUtc(DateTimeOffset valor, string campo) =>
        valor.Offset == TimeSpan.Zero
            ? valor
            : throw new ReglaDominioException(campo, $"{campo} debe expresarse en UTC.");

    public static DateTimeOffset? ExigirUtc(DateTimeOffset? valor, string campo) =>
        valor is { } instante ? ExigirUtc(instante, campo) : null;
}
