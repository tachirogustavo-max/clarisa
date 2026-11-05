using Clarisa.Domain.Common;
using Clarisa.Domain.Exceptions;

namespace Clarisa.Domain.Entities;

public enum EstadoPolitica
{
    Borrador = 1,
    Vigente = 2,
    Reemplazada = 3
}

public enum FormulaDefinitiva
{
    PromedioLapsos = 1,
    PonderadaPorLapso = 2
}

public enum ModoRedondeo
{
    Truncar = 1,
    MitadHaciaArriba = 2,
    MitadAlPar = 3
}

public enum TratamientoExonerado
{
    ResultadoPendiente = 1,
    ExcluirLapso = 2
}

// Quién y cuándo, con el reloj del servidor. La activación conserva su propio motivo.
public sealed record TrazaPolitica(Guid CreadaPor, DateTimeOffset CreadaEn, Guid? ActivadaPor = null, DateTimeOffset? ActivadaEn = null,
    string? MotivoActivacion = null);

public sealed record ReglasPolitica(int? DecimalesCaptura, int? DecimalesResultado, decimal? NotaAprobatoria, FormulaDefinitiva? Formula,
    ModoRedondeo? Redondeo, TratamientoExonerado? Exonerado, decimal? PesoLapso1, decimal? PesoLapso2, decimal? PesoLapso3);

// Reglas de cálculo de un año que elige la dirección. Ningún valor tiene predeterminado: lo que falta queda nulo y deja pendiente
// el resultado que dependa de él. Solo un borrador cambia; una vigente se sustituye activando otro borrador.
public sealed class PoliticaAcademica : Entidad
{
    public const int LapsosDelAnio = 3;
    public const int MaximoMotivo = 200;

    public PoliticaAcademica(Guid id, Guid anioLectivoId, int numero, EstadoPolitica estado, ReglasPolitica reglas, string? motivo,
        TrazaPolitica traza, long version) : base(id, version)
    {
        AnioLectivoId = Requerido(anioLectivoId, "AnioLectivoId", "el año escolar");
        if (numero < 1) throw new ReglaDominioException("Numero", "El número de la política no es válido.");
        if (!Enum.IsDefined(estado)) throw new ReglaDominioException("Estado", "El estado de la política no es válido.");
        Numero = numero;
        Estado = estado;
        Reglas = Validar(reglas);
        Motivo = Textos.Requerido(motivo, "Motivo", MaximoMotivo);
        Traza = traza ?? throw new ReglaDominioException("Traza", "Falta la trazabilidad de la política.");
        if (traza.CreadaPor == Guid.Empty) throw new ReglaDominioException("Traza", "Falta quién creó la política.");
        if ((estado == EstadoPolitica.Borrador) != (traza.ActivadaPor is null))
            throw new ReglaDominioException("Traza", "La activación registrada no coincide con el estado de la política.");
    }

    public Guid AnioLectivoId { get; }
    public int Numero { get; }
    public EstadoPolitica Estado { get; private set; }
    public ReglasPolitica Reglas { get; private set; }
    public string Motivo { get; private set; }
    public TrazaPolitica Traza { get; private set; }

    public IReadOnlyList<string> Faltantes => [
        .. Reglas.DecimalesCaptura is null ? ["Decimales de las notas de momento"] : Array.Empty<string>(),
        .. Reglas.DecimalesResultado is null ? ["Decimales de la definitiva"] : Array.Empty<string>(),
        .. Reglas.Formula is null ? ["Fórmula de la definitiva"] : Array.Empty<string>(),
        .. Reglas.Redondeo is null ? ["Regla de redondeo"] : Array.Empty<string>(),
        .. Reglas.Exonerado is null ? ["Tratamiento de un momento exonerado"] : Array.Empty<string>(),
        .. Reglas.Formula == FormulaDefinitiva.PonderadaPorLapso && !PesosCompletos(Reglas) ? ["Pesos de los tres momentos"] : Array.Empty<string>()
    ];

    public bool Completa => Faltantes.Count == 0;

    public void Editar(ReglasPolitica reglas, string? motivo)
    {
        if (Estado != EstadoPolitica.Borrador) throw new ReglaDominioException("Estado", "Solo se modifica una política en borrador.");
        Reglas = Validar(reglas);
        Motivo = Textos.Requerido(motivo, "Motivo", MaximoMotivo);
    }

    public void Activar(Guid actor, DateTimeOffset ahoraUtc, string? motivo)
    {
        if (Estado != EstadoPolitica.Borrador) throw new ReglaDominioException("Estado", "Solo se activa una política en borrador.");
        if (!Completa) throw new ReglaDominioException("Reglas", "Completa la política antes de activarla: " + string.Join(", ", Faltantes) + ".");
        var motivoValido = Textos.Requerido(motivo, "Motivo", MaximoMotivo);
        Estado = EstadoPolitica.Vigente;
        Traza = Traza with { ActivadaPor = actor, ActivadaEn = Instantes.ExigirUtc(ahoraUtc, "ActivadaEn"), MotivoActivacion = motivoValido };
    }

    public void Reemplazar()
    {
        if (Estado != EstadoPolitica.Vigente) throw new ReglaDominioException("Estado", "Solo se reemplaza la política vigente.");
        Estado = EstadoPolitica.Reemplazada;
    }

    private static ReglasPolitica Validar(ReglasPolitica? reglas)
    {
        if (reglas is null) throw new ReglaDominioException("Reglas", "Faltan las reglas de la política.");
        Decimales(reglas.DecimalesCaptura, "DecimalesCaptura");
        Decimales(reglas.DecimalesResultado, "DecimalesResultado");
        if (reglas.NotaAprobatoria is { } aprobatoria)
            EscalaNotas.Validar(aprobatoria, EscalaNotas.DecimalesTecnicosMaximos, "NotaAprobatoria");
        if (reglas.Formula is { } formula && !Enum.IsDefined(formula))
            throw new ReglaDominioException("Formula", "Selecciona una fórmula de la lista.");
        if (reglas.Redondeo is { } redondeo && !Enum.IsDefined(redondeo))
            throw new ReglaDominioException("Redondeo", "Selecciona una regla de redondeo de la lista.");
        if (reglas.Exonerado is { } exonerado && !Enum.IsDefined(exonerado))
            throw new ReglaDominioException("Exonerado", "Selecciona cómo se trata un momento exonerado.");
        var pesos = new[] { (reglas.PesoLapso1, "PesoLapso1"), (reglas.PesoLapso2, "PesoLapso2"), (reglas.PesoLapso3, "PesoLapso3") };
        foreach (var (peso, campo) in pesos)
            if (peso is { } valor && (valor <= 0 || valor > 100 || decimal.Round(valor, 2) != valor))
                throw new ReglaDominioException(campo, "Cada peso va de 0,01 a 100 con hasta dos decimales.");
        if (reglas.Formula != FormulaDefinitiva.PonderadaPorLapso && pesos.Any(p => p.Item1 is not null))
            throw new ReglaDominioException("PesoLapso1", "Los pesos solo se indican con la fórmula ponderada.");
        if (PesosCompletos(reglas) && reglas.PesoLapso1 + reglas.PesoLapso2 + reglas.PesoLapso3 != 100m)
            throw new ReglaDominioException("PesoLapso3", "Los pesos de los tres momentos deben sumar 100.");
        return reglas;
    }

    private static bool PesosCompletos(ReglasPolitica reglas) =>
        reglas.PesoLapso1 is not null && reglas.PesoLapso2 is not null && reglas.PesoLapso3 is not null;

    private static void Decimales(int? valor, string campo)
    {
        if (valor is < 0 or > EscalaNotas.DecimalesTecnicosMaximos)
            throw new ReglaDominioException(campo, $"Los decimales van de 0 a {EscalaNotas.DecimalesTecnicosMaximos}.");
    }
}
