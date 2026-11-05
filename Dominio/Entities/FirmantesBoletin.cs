using Clarisa.Domain.Common;
using Clarisa.Domain.Exceptions;

namespace Clarisa.Domain.Entities;

// Nombres impresos bajo las firmas del boletín. Cada cambio es un registro nuevo con su número; los anteriores no se tocan.
public sealed class FirmantesBoletin : Entidad
{
    public const int MaximoNombre = 120;
    public const int MaximoMotivo = 200;

    public FirmantesBoletin(Guid id, int numero, string? director, string? coordinadorEvaluacion, string? coordinadorEstudios,
        string? motivo, Guid registradoPor, DateTimeOffset registradoEn) : base(id, numero)
    {
        Numero = numero;
        Director = Textos.Opcional(director, "Director", MaximoNombre);
        CoordinadorEvaluacion = Textos.Opcional(coordinadorEvaluacion, "Coordinación de evaluación", MaximoNombre);
        CoordinadorEstudios = Textos.Opcional(coordinadorEstudios, "Coordinación de control de estudio", MaximoNombre);
        Motivo = Textos.Requerido(motivo, "Motivo", MaximoMotivo);
        RegistradoPor = Requerido(registradoPor, "RegistradoPor", "quién registra el cambio");
        RegistradoEn = Instantes.ExigirUtc(registradoEn, "RegistradoEn");
        if (Director is null && CoordinadorEvaluacion is null && CoordinadorEstudios is null)
            throw new ReglaDominioException("Director", "Escribe al menos un nombre para las firmas.");
    }

    public int Numero { get; }
    public string? Director { get; }
    public string? CoordinadorEvaluacion { get; }
    public string? CoordinadorEstudios { get; }
    public string Motivo { get; }
    public Guid RegistradoPor { get; }
    public DateTimeOffset RegistradoEn { get; }
}
