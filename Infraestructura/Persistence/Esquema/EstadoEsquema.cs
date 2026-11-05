namespace Clarisa.Infrastructure.Persistence.Esquema;

public enum EstadoEsquema
{
    Vacia = 1,
    Pendiente = 2,
    AlDia = 3,
    MasNueva = 4,
    Ajena = 5,
    Alterada = 6
}

// Fallo técnico (conexión, permisos, tiempo agotado): puede repetirse; las sentencias ya ejecutadas son repetibles.
public sealed class EsquemaNoDisponibleException(string mensaje, Exception causa) : Exception(mensaje, causa);

// Decisión: la base no pertenece a esta aplicación, es más nueva o fue alterada. No se repite sin intervención humana.
public sealed class MigracionRechazadaException(string mensaje, EstadoEsquema? estado, IReadOnlyList<string> discrepancias,
    Exception? causa = null) : Exception(mensaje, causa)
{
    public EstadoEsquema? Estado { get; } = estado;
    public IReadOnlyList<string> Discrepancias { get; } = discrepancias;
}
