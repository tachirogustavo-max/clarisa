using Clarisa.Domain.Common;

namespace Clarisa.Domain.Entities;

public sealed class Seccion : Entidad
{
    public const int MaximoGrado = 40;
    public const int MaximoNombre = 20;

    public Seccion(Guid id, Guid anioLectivoId, string? grado, string? nombre, long version) : base(id, version)
    {
        AnioLectivoId = Requerido(anioLectivoId, "AnioLectivoId", "el año lectivo");
        Grado = Textos.Requerido(grado, "Grado", MaximoGrado);
        Nombre = Textos.Requerido(nombre, "Nombre", MaximoNombre);
    }

    public Guid AnioLectivoId { get; }
    public string Grado { get; private set; }
    public string Nombre { get; private set; }

    public void Modificar(string? grado, string? nombre)
    {
        var nuevoGrado = Textos.Requerido(grado, "Grado", MaximoGrado);
        var nuevoNombre = Textos.Requerido(nombre, "Nombre", MaximoNombre);
        Grado = nuevoGrado;
        Nombre = nuevoNombre;
    }

    public bool MismoContenido(Seccion otro) =>
        AnioLectivoId == otro.AnioLectivoId && Textos.Iguales(Grado, otro.Grado) && Textos.Iguales(Nombre, otro.Nombre);
}
