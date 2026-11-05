using Clarisa.Domain.Common;
using Clarisa.Domain.Exceptions;

namespace Clarisa.Domain.Entities;

// Año de estudio del liceo (1er año, 2do año...). Las secciones se crean dentro de un año activo.
public sealed class Grado : Entidad
{
    public const int MaximoNombre = 40;
    public const int MaximoOrden = 99;

    public Grado(Guid id, string? nombre, int orden, bool activo, long version) : base(id, version)
    {
        Nombre = Textos.Requerido(nombre, "Nombre", MaximoNombre);
        Orden = ValidarOrden(orden);
        Activo = activo;
    }

    public string Nombre { get; private set; }
    public int Orden { get; private set; }
    public bool Activo { get; private set; }

    public void Modificar(string? nombre, int orden, bool activo)
    {
        var nuevoNombre = Textos.Requerido(nombre, "Nombre", MaximoNombre);
        Orden = ValidarOrden(orden);
        Nombre = nuevoNombre;
        Activo = activo;
    }

    public bool MismoContenido(Grado otro) => Textos.Iguales(Nombre, otro.Nombre) && Orden == otro.Orden && Activo == otro.Activo;

    private static int ValidarOrden(int orden) => orden is >= 1 and <= MaximoOrden ? orden
        : throw new ReglaDominioException("Orden", $"La posición debe ser un número del 1 al {MaximoOrden}.");
}
