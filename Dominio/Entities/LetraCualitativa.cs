using Clarisa.Domain.Common;
using Clarisa.Domain.Exceptions;

namespace Clarisa.Domain.Entities;

// Solo forma técnica de la letra: qué letras existen y qué significan lo define la dirección para cada año.
public sealed class LetraCualitativa : Entidad
{
    public const int MaximoLetra = 4;
    public const int MaximoDescripcion = 100;

    public LetraCualitativa(Guid id, Guid anioLectivoId, string? letra, string? descripcion, long version) : base(id, version)
    {
        AnioLectivoId = Requerido(anioLectivoId, "AnioLectivoId", "el año lectivo");
        Letra = Normalizar(letra);
        Descripcion = Textos.Opcional(descripcion, "Descripcion", MaximoDescripcion);
    }

    public Guid AnioLectivoId { get; }
    public string Letra { get; }
    public string? Descripcion { get; }

    public bool MismoContenido(LetraCualitativa otra) =>
        AnioLectivoId == otra.AnioLectivoId && Letra == otra.Letra && Descripcion == otra.Descripcion;

    public static string Normalizar(string? letra)
    {
        var limpia = (letra ?? "").Trim().ToUpperInvariant();
        if (limpia.Length is 0 or > MaximoLetra || !limpia.All(c => c is >= 'A' and <= 'Z'))
            throw new ReglaDominioException("Letra", $"Letra: usa de 1 a {MaximoLetra} letras sin acento.");
        return limpia;
    }
}
