using Clarisa.Domain.Common;

namespace Clarisa.Domain.Entities;

public sealed class AnioLectivo : Entidad
{
    public const int MaximoNombre = 40;

    public AnioLectivo(Guid id, string? nombre, DateOnly inicio, DateOnly fin, Guid? lapsoActualId, long version) : base(id, version)
    {
        Nombre = Textos.Requerido(nombre, "Nombre", MaximoNombre);
        Fechas = new RangoFechas(inicio, fin, admiteMismoDia: false, "Fechas");
        LapsoActualId = lapsoActualId == Guid.Empty ? null : lapsoActualId;
    }

    public string Nombre { get; private set; }
    public RangoFechas Fechas { get; private set; }
    public Guid? LapsoActualId { get; private set; }

    public void Modificar(string? nombre, DateOnly inicio, DateOnly fin)
    {
        var nuevoNombre = Textos.Requerido(nombre, "Nombre", MaximoNombre);
        var nuevasFechas = new RangoFechas(inicio, fin, admiteMismoDia: false, "Fechas");
        Nombre = nuevoNombre;
        Fechas = nuevasFechas;
    }

    public void SeleccionarLapsoActual(Guid? lapsoId) => LapsoActualId = lapsoId == Guid.Empty ? null : lapsoId;

    public bool MismoContenido(AnioLectivo otro) =>
        Textos.Iguales(Nombre, otro.Nombre) && Fechas == otro.Fechas && LapsoActualId == otro.LapsoActualId;
}
