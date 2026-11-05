namespace Clarisa.Domain.Entities;

// Promedio de la fila PROMEDIO del boletín: media de las notas numéricas del estudiante en todas sus materias. No cuentan las
// materias exoneradas, las no cursadas ni las evaluadas con letra, aunque estas lleven también un número (por ejemplo «19 (A)»).
// Si a una materia que cuenta le falta el dato, el promedio queda pendiente. Se redondea como indica la política del año.
public static class CalculoPromedio
{
    public static Definitiva DeMomento(PoliticaAcademica? politica,
        IEnumerable<(ModalidadEvaluacion? Modalidad, TipoResultado Tipo, decimal? Valor)> notas)
    {
        var valores = new List<decimal>();
        foreach (var (modalidad, tipo, valor) in notas)
        {
            if (!Cuenta(modalidad)) continue;
            if (tipo == TipoResultado.Pendiente) return Definitiva.Pendiente;
            if (tipo == TipoResultado.Numerico && valor is { } numero) valores.Add(numero);
        }
        return Resultado(politica, valores);
    }

    // Promedio de las definitivas, para la columna DEFIN. Solo tiene sentido con los resultados cerrados.
    public static Definitiva DeDefinitivas(PoliticaAcademica? politica,
        IEnumerable<(ModalidadEvaluacion? Modalidad, Definitiva Definitiva)> definitivas)
    {
        var valores = new List<decimal>();
        foreach (var (modalidad, definitiva) in definitivas)
        {
            if (!Cuenta(modalidad)) continue;
            if (definitiva.Estado == EstadoDefinitiva.Pendiente) return Definitiva.Pendiente;
            if (definitiva.Estado == EstadoDefinitiva.Calculada && definitiva.Valor is { } numero) valores.Add(numero);
        }
        return Resultado(politica, valores);
    }

    // Sin plan del año la materia se trata como numérica, igual que en la carga de notas.
    private static bool Cuenta(ModalidadEvaluacion? modalidad) => modalidad is null or ModalidadEvaluacion.Numerica;

    private static Definitiva Resultado(PoliticaAcademica? politica, List<decimal> valores)
    {
        if (valores.Count == 0) return new(EstadoDefinitiva.NoNumerica, null);
        if (politica is not { Completa: true }) return Definitiva.Pendiente;
        var reglas = politica.Reglas;
        return new(EstadoDefinitiva.Calculada, CalculoDefinitiva.Redondear(valores.Average(), reglas.DecimalesResultado!.Value,
            reglas.Redondeo!.Value));
    }
}
