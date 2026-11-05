namespace Clarisa.Instalacion.Ejecucion;

internal enum TipoComprobacion
{
    Satisfecha,
    NoSatisfecha,
    Indeterminada,
    Omitida
}

internal sealed record Comprobacion(TipoComprobacion Tipo, string Detalle)
{
    public static Comprobacion Satisfecha(string detalle) => new(TipoComprobacion.Satisfecha, detalle);
    public static Comprobacion NoSatisfecha(string detalle) => new(TipoComprobacion.NoSatisfecha, detalle);
    public static Comprobacion Indeterminada(string detalle) => new(TipoComprobacion.Indeterminada, detalle);
    public static Comprobacion Omitida(string detalle) => new(TipoComprobacion.Omitida, detalle);
}

// Comprobar es de solo lectura y describe la postcondición real; Aplicar produce el efecto. El ejecutor no da por bueno un paso
// porque Aplicar terminara: siempre vuelve a comprobar.
internal interface IPasoInstalacion
{
    string Id { get; }
    string Nombre { get; }
    string Descripcion { get; }
    bool Critico { get; }
    bool Privilegiado { get; }

    Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct);

    Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct);
}

// Fallo esperado con un mensaje que puede mostrarse y registrarse: nunca contiene secretos ni salidas completas de procesos.
internal sealed class FalloInstalacionException(string mensaje, Exception? causa = null) : Exception(mensaje, causa);
