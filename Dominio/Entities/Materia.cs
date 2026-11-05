using Clarisa.Domain.Common;
using Clarisa.Domain.Exceptions;

namespace Clarisa.Domain.Entities;

public sealed class Materia : Entidad
{
    public const int MaximoCodigo = 20;
    public const int MaximoNombre = 100;

    public Materia(Guid id, string? codigo, string? nombre, bool activa, long version) : base(id, version)
    {
        Codigo = NormalizarCodigo(codigo);
        Nombre = Textos.Requerido(nombre, "Nombre", MaximoNombre);
        Activa = activa;
    }

    public string Codigo { get; private set; }
    public string Nombre { get; private set; }
    public bool Activa { get; private set; }

    public void Modificar(string? codigo, string? nombre, bool activa)
    {
        var nuevoCodigo = NormalizarCodigo(codigo);
        var nuevoNombre = Textos.Requerido(nombre, "Nombre", MaximoNombre);
        Codigo = nuevoCodigo;
        Nombre = nuevoNombre;
        Activa = activa;
    }

    public bool MismoContenido(Materia otro) => Codigo == otro.Codigo && Nombre == otro.Nombre && Activa == otro.Activa;

    public static string NormalizarCodigo(string? codigo)
    {
        var limpio = (codigo ?? "").Trim().ToUpperInvariant();
        if (limpio.Length is 0 or > MaximoCodigo || !limpio.All(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-'))
            throw new ReglaDominioException("Codigo", $"Código: usa de 1 a {MaximoCodigo} letras sin acento, números o guiones.");
        return limpio;
    }
}
