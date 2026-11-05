namespace Clarisa.Application.Features.Cuentas;

// Acota cuántas derivaciones de clave del inicio de sesión se calculan a la vez. Sin este tope, muchas solicitudes simultáneas
// (desde varias estaciones o direcciones) ocuparían toda la CPU del servidor y retrasarían también a quienes ya trabajan.
public sealed class TurnoVerificacionClaves : IDisposable
{
    private readonly SemaphoreSlim turnos;
    private readonly TimeSpan esperaMaxima;

    public TurnoVerificacionClaves(OpcionesLimiteIntentos opciones)
    {
        var validas = opciones.Validar();
        turnos = new SemaphoreSlim(validas.VerificacionesSimultaneas, validas.VerificacionesSimultaneas);
        esperaMaxima = validas.EsperaVerificacion;
    }

    public int Disponibles => turnos.CurrentCount;

    // Null si no hubo turno dentro de la espera máxima: el llamador responde que reintente sin calcular nada.
    public async Task<IDisposable?> EntrarAsync(CancellationToken ct) =>
        await turnos.WaitAsync(esperaMaxima, ct) ? new Turno(turnos) : null;

    public void Dispose() => turnos.Dispose();

    private sealed class Turno(SemaphoreSlim turnos) : IDisposable
    {
        private int liberado;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref liberado, 1) == 0) turnos.Release();
        }
    }
}
