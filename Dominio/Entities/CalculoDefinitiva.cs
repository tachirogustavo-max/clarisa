namespace Clarisa.Domain.Entities;

public enum EstadoDefinitiva
{
    Pendiente = 0,
    Calculada = 1,
    NoCursada = 2,
    Exonerada = 3,
    NoNumerica = 4
}

// Tipo nulo: el lapso no tiene fila guardada, que equivale a pendiente; nunca a cero.
public sealed record ResultadoLapso(int Orden, TipoResultado? Tipo, decimal? Valor);

public sealed record Definitiva(EstadoDefinitiva Estado, decimal? Valor)
{
    public static readonly Definitiva Pendiente = new(EstadoDefinitiva.Pendiente, null);
}

// Definitiva anual de una materia según una política completa. Las letras nunca se convierten en números y un dato que falta deja
// el resultado pendiente; no hay valores por omisión.
public static class CalculoDefinitiva
{
    public static Definitiva Calcular(PoliticaAcademica? politica, ModalidadEvaluacion? modalidad, IReadOnlyList<ResultadoLapso> lapsos)
    {
        if (modalidad == ModalidadEvaluacion.NoCursada) return new(EstadoDefinitiva.NoCursada, null);
        if (modalidad == ModalidadEvaluacion.Cualitativa) return new(EstadoDefinitiva.NoNumerica, null);
        if (politica is not { Completa: true } || lapsos.Count != PoliticaAcademica.LapsosDelAnio
            || lapsos.Select(l => l.Orden).Order().SequenceEqual([1, 2, 3]) is false)
            return Definitiva.Pendiente;

        var reglas = politica.Reglas;
        var incluidos = new List<(decimal Valor, decimal Peso)>(PoliticaAcademica.LapsosDelAnio);
        foreach (var lapso in lapsos.OrderBy(l => l.Orden))
        {
            switch (lapso.Tipo)
            {
                case TipoResultado.Numerico or TipoResultado.NumericoYCualitativo when lapso.Valor is { } valor:
                    incluidos.Add((valor, Peso(reglas, lapso.Orden)));
                    break;
                case TipoResultado.Exonerado when reglas.Exonerado == TratamientoExonerado.ExcluirLapso:
                    break;
                default:
                    return Definitiva.Pendiente;
            }
        }
        if (incluidos.Count == 0) return new(EstadoDefinitiva.Exonerada, null);
        var bruto = reglas.Formula == FormulaDefinitiva.PonderadaPorLapso
            ? incluidos.Sum(i => i.Valor * i.Peso) / incluidos.Sum(i => i.Peso)
            : incluidos.Average(i => i.Valor);
        return new(EstadoDefinitiva.Calculada, Redondear(bruto, reglas.DecimalesResultado!.Value, reglas.Redondeo!.Value));
    }

    // MidpointRounding siempre explícito: el predeterminado de .NET (al par) no debe aplicarse sin que la dirección lo elija.
    public static decimal Redondear(decimal valor, int decimales, ModoRedondeo modo)
    {
        var factor = (decimal)Math.Pow(10, decimales);
        return modo switch
        {
            ModoRedondeo.Truncar => decimal.Truncate(valor * factor) / factor,
            ModoRedondeo.MitadHaciaArriba => decimal.Round(valor, decimales, MidpointRounding.AwayFromZero),
            ModoRedondeo.MitadAlPar => decimal.Round(valor, decimales, MidpointRounding.ToEven),
            _ => throw new ArgumentOutOfRangeException(nameof(modo))
        };
    }

    private static decimal Peso(ReglasPolitica reglas, int orden) => reglas.Formula != FormulaDefinitiva.PonderadaPorLapso ? 1m : orden switch
    {
        1 => reglas.PesoLapso1!.Value,
        2 => reglas.PesoLapso2!.Value,
        _ => reglas.PesoLapso3!.Value
    };
}
