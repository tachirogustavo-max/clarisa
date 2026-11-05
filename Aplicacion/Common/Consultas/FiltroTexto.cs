using Clarisa.Application.Common.Errores;
using Clarisa.Domain.Common;

namespace Clarisa.Application.Common.Consultas;

public sealed record FiltroTexto(string? Texto, bool IncluirInactivos)
{
    public const int MaximoTexto = 80;

    public static FiltroTexto Validar(FiltroTexto? filtro)
    {
        var valido = Error.SolicitudRequerida(filtro);
        return valido with { Texto = Error.DesdeDominio(() => Textos.Opcional(valido.Texto, "Texto", MaximoTexto)) };
    }
}
