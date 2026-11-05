using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Application.Common.Errores;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence;

internal sealed class ConfirmacionInciertaException(Exception causa)
    : Exception("Falló la confirmación; el servidor pudo haberla aplicado o no.", causa);

// Ciclo de vida común de una conexión y su transacción: turno de escritura, confirmación única y descarte seguro.
// Lo comparten la unidad académica y la de seguridad para que ambas sigan el mismo orden de bloqueos.
internal abstract class UnidadMariaDb : IAsyncDisposable
{
    private readonly MySqlConnection conexion;
    private VerificadorSesionEnUnidad? acceso;

    protected UnidadMariaDb(MySqlConnection conexion, MySqlTransaction transaccion, ModoUnidadTrabajo modo, int segundosComando)
    {
        this.conexion = conexion;
        Sesion = new SesionMariaDb(conexion, transaccion, modo, segundosComando);
    }

    protected SesionMariaDb Sesion { get; }

    internal int IdConexion => Sesion.IdConexion;

    internal void VincularAcceso(ReferenciaSesionActual referencia, IReloj reloj)
    {
        if (acceso is not null) throw new InvalidOperationException("La identidad de la unidad ya está vinculada.");
        acceso = new(Sesion, referencia, reloj);
    }
    internal Task ValidarAccesoAsync(CancellationToken ct) => acceso?.ExigirAsync(ct) ?? Task.CompletedTask;

    // Todo escritor toma primero esta fila: cierre de ventana y guardado de notas, como cualquier otra escritura, quedan en orden total.
    internal async Task TomarTurnoEscrituraAsync(CancellationToken ct)
    {
        var turno = await Sesion.EnteroAsync("SELECT id FROM bloqueo_escritura WHERE id = 1 FOR UPDATE", ct);
        if (turno != 1)
        {
            Sesion.Marcar(EstadoSesion.Fallida);
            throw Error.AlmacenNoDisponible(new DatoAlmacenadoInvalidoException("Falta la fila de turno de escritura: esquema incompleto."));
        }
    }

    public async Task ConfirmarAsync(CancellationToken ct)
    {
        Sesion.ExigirActiva();
        if (Sesion.Modo != ModoUnidadTrabajo.Escritura) throw new InvalidOperationException("Una unidad de lectura no confirma cambios.");
        ct.ThrowIfCancellationRequested();
        acceso?.ExigirVigencia();
        // Sin token: una cancelación que llegara durante COMMIT dejaría el resultado en duda.
        try
        {
            await Sesion.Transaccion.CommitAsync(CancellationToken.None);
            Sesion.Marcar(EstadoSesion.Confirmada);
        }
        catch (Exception error) when (error is MySqlException || TraduccionErrores.EsFalloTecnico(error))
        {
            Sesion.MarcarDudosa();
            throw Error.ResultadoDesconocido(new ConfirmacionInciertaException(error));
        }
    }

    // Nunca lanza: desechar no debe reemplazar la excepción que causó la salida del caso de uso.
    public async ValueTask DisposeAsync()
    {
        if (Sesion.Estado == EstadoSesion.Cerrada) return;
        var revertir = Sesion.Estado != EstadoSesion.Confirmada;
        var dudosa = Sesion.ConexionDudosa;
        Sesion.Marcar(EstadoSesion.Cerrada);
        if (revertir && !dudosa) dudosa = !await IntentarAsync(() => Sesion.Transaccion.RollbackAsync(CancellationToken.None));
        await IntentarAsync(() => Sesion.Transaccion.DisposeAsync().AsTask());
        // La conexión dudosa no vuelve al pool; el resultado de COMMIT debe reconciliarse antes de repetir la operación.
        if (dudosa) await IntentarAsync(() => MySqlConnection.ClearPoolAsync(conexion));
        await IntentarAsync(() => conexion.DisposeAsync().AsTask());
    }

    private static async Task<bool> IntentarAsync(Func<Task> accion)
    {
        try
        {
            await accion();
            return true;
        }
        catch (Exception error) when (error is MySqlException || TraduccionErrores.EsFalloTecnico(error))
        {
            return false;
        }
    }
}
