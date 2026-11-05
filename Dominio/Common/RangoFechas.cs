using Clarisa.Domain.Exceptions;

namespace Clarisa.Domain.Common;

public sealed record RangoFechas
{
    public RangoFechas(DateOnly inicio, DateOnly fin, bool admiteMismoDia, string campo)
    {
        if (fin < inicio || (!admiteMismoDia && fin == inicio))
            throw new ReglaDominioException(campo, $"{campo}: la fecha final debe ser posterior a la inicial.");
        Inicio = inicio;
        Fin = fin;
    }

    public DateOnly Inicio { get; }
    public DateOnly Fin { get; }

    public bool Contiene(DateOnly fecha) => fecha >= Inicio && fecha <= Fin;

    public bool Contiene(RangoFechas otro) => otro.Inicio >= Inicio && otro.Fin <= Fin;
}
