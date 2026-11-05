namespace Clarisa.Application.Features.Respaldos;

// Instancia única por proceso: serializa la aceptación de solicitudes y despierta al trabajador. No guarda estado de trabajos.
public sealed class CoordinacionRespaldos
{
    private readonly SemaphoreSlim aceptacion = new(1, 1);
    private readonly SemaphoreSlim aviso = new(0, 1);
    private volatile bool lista;

    public bool Lista => lista;

    public void MarcarLista() => lista = true;

    public async Task<IDisposable> TomarAceptacionAsync(CancellationToken ct)
    {
        await aceptacion.WaitAsync(ct);
        return new Liberacion(aceptacion);
    }

    public void Avisar()
    {
        try
        {
            aviso.Release();
        }
        catch (SemaphoreFullException)
        {
            // Ya hay un aviso pendiente: el trabajador revisará todas las operaciones abiertas al despertar.
        }
    }

    public Task EsperarAvisoAsync(TimeSpan maximo, CancellationToken ct) => aviso.WaitAsync(maximo, ct);

    private sealed class Liberacion(SemaphoreSlim semaforo) : IDisposable
    {
        private int liberada;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref liberada, 1) == 0) semaforo.Release();
        }
    }
}
