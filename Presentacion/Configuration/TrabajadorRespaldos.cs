using Clarisa.Application.Common.Errores;
using Clarisa.Application.Features.Respaldos;
using Clarisa.Infrastructure.Configuration;

namespace Clarisa.Api.Configuration;

// Un único trabajador por proceso: reconcilia lo que quedó abierto y luego procesa de a una copia. Al detenerse el servidor, la copia en
// curso termina Fallida sin publicar archivo. Una cancelación que no viene de la detención se trata como fallo, no como fin del trabajador.
public sealed class TrabajadorRespaldos(EjecucionRespaldos ejecucion, CoordinacionRespaldos coordinacion, ConfiguracionRespaldos configuracion,
    IClaveRespaldos clave, CopiasAutomaticas automaticas, ILogger<TrabajadorRespaldos> registro) : BackgroundService
{
    private static readonly TimeSpan Revision = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        try
        {
            await TrabajarAsync(detener);
        }
        finally
        {
            registro.LogInformation("Trabajador de respaldos detenido.");
        }
    }

    private async Task TrabajarAsync(CancellationToken detener)
    {
        await RevisarClaveAsync(detener);
        while (!detener.IsCancellationRequested && !coordinacion.Lista)
        {
            try
            {
                configuracion.CrearDirectorios();
                var revisadas = await ejecucion.ReconciliarAsync(detener);
                if (revisadas > 0) registro.LogWarning("Se reconciliaron {Cantidad} operaciones de respaldo abiertas al iniciar.", revisadas);
                registro.LogInformation("Servicio de copias disponible.");
            }
            catch (Exception error) when (!detener.IsCancellationRequested)
            {
                registro.LogError("No se pudo revisar el registro de respaldos; no se aceptarán copias hasta resolverlo ({Tipo}): {Detalle}",
                    error.GetType().Name, Detalle(error));
                await EsperarAsync(Revision, detener);
            }
        }
        while (!detener.IsCancellationRequested)
        {
            try
            {
                if (await automaticas.RevisarAsync(configuracion.ReglasAutomaticas(), TimeZoneInfo.Local, detener) is { } automatica)
                    registro.LogInformation("Copia automática diaria solicitada ({Operacion}).", automatica);
                foreach (var id in await ejecucion.PendientesAsync(detener)) await ProcesarAsync(id, detener);
            }
            catch (Exception error) when (!detener.IsCancellationRequested)
            {
                registro.LogError("No se pudieron leer las operaciones de respaldo pendientes ({Tipo}): {Detalle}", error.GetType().Name,
                    Detalle(error));
            }
            await EsperarAsync(Revision, detener);
        }
    }

    private async Task RevisarClaveAsync(CancellationToken detener)
    {
        try
        {
            if (!await clave.EstaPreparadaAsync(detener))
                registro.LogWarning("La clave de respaldos no está preparada: el acceso académico sigue disponible, pero se rechazarán las copias.");
        }
        catch (Exception error) when (!detener.IsCancellationRequested)
        {
            registro.LogError("No se pudo comprobar la clave de respaldos ({Tipo}).", error.GetType().Name);
        }
    }

    private async Task ProcesarAsync(Guid id, CancellationToken detener)
    {
        try
        {
            await ejecucion.ProcesarAsync(id, detener);
        }
        catch (Exception error) when (!detener.IsCancellationRequested)
        {
            registro.LogError("La operación de respaldo {Operacion} falló ({Tipo}).", id, error.GetType().Name);
            try
            {
                await ejecucion.RevisarAsync(id, detener);
            }
            catch (Exception revision) when (!detener.IsCancellationRequested)
            {
                registro.LogError("No se pudo cerrar la operación {Operacion} ({Tipo}); se revisará al reiniciar.", id, revision.GetType().Name);
            }
        }
    }

    // Mensajes propios o del sistema de archivos (rutas del servidor, sin claves); de otras excepciones solo se registra el tipo.
    private static string Detalle(Exception error) =>
        error is ErrorAplicacionException or IOException or UnauthorizedAccessException ? error.Message : "sin detalle";

    private async Task EsperarAsync(TimeSpan maximo, CancellationToken detener)
    {
        try
        {
            await coordinacion.EsperarAvisoAsync(maximo, detener);
        }
        catch (OperationCanceledException) when (detener.IsCancellationRequested)
        {
        }
    }
}
