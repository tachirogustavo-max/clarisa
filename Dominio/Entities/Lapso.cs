using Clarisa.Domain.Common;
using Clarisa.Domain.Exceptions;

namespace Clarisa.Domain.Entities;

public sealed class Lapso : Entidad
{
    public const int MaximoNombre = 60;
    public const int CantidadPorAnio = 3;

    public Lapso(Guid id, Guid anioLectivoId, string? nombre, int orden, DateOnly inicio, DateOnly fin, long version)
        : base(id, version)
    {
        AnioLectivoId = Requerido(anioLectivoId, "AnioLectivoId", "el año lectivo");
        Nombre = Textos.Requerido(nombre, "Nombre", MaximoNombre);
        Orden = ValidarOrden(orden);
        Fechas = new RangoFechas(inicio, fin, admiteMismoDia: true, "Fechas");
    }

    public Guid AnioLectivoId { get; }
    public string Nombre { get; private set; }
    public int Orden { get; private set; }
    public RangoFechas Fechas { get; private set; }

    public void Modificar(string? nombre, int orden, DateOnly inicio, DateOnly fin)
    {
        var nuevoNombre = Textos.Requerido(nombre, "Nombre", MaximoNombre);
        var nuevoOrden = ValidarOrden(orden);
        var nuevasFechas = new RangoFechas(inicio, fin, admiteMismoDia: true, "Fechas");
        Nombre = nuevoNombre;
        Orden = nuevoOrden;
        Fechas = nuevasFechas;
    }

    public bool MismoContenido(Lapso otro) =>
        AnioLectivoId == otro.AnioLectivoId && Textos.Iguales(Nombre, otro.Nombre) && Orden == otro.Orden && Fechas == otro.Fechas;

    private static int ValidarOrden(int orden) =>
        orden is < 1 or > CantidadPorAnio
            ? throw new ReglaDominioException("Orden", $"El año escolar tiene {CantidadPorAnio} lapsos: el orden debe estar entre 1 y {CantidadPorAnio}.")
            : orden;
}
