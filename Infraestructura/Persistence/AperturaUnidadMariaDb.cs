using System.Data;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Errores;
using Clarisa.Infrastructure.Configuration;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence;

// Una conexión del pool por unidad. Lectura: instantánea coherente de solo lectura. Escritura: READ COMMITTED con turno exclusivo.
internal sealed class AperturaUnidadMariaDb
{
    internal const string ModoSql = "STRICT_ALL_TABLES,NO_ZERO_DATE,NO_ZERO_IN_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION";

    private readonly ConfiguracionMariaDb configuracion;
    private readonly string cadenaConexion;

    public AperturaUnidadMariaDb(ConfiguracionMariaDb configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);
        this.configuracion = configuracion;
        cadenaConexion = configuracion.CadenaConexion();
    }

    public async Task<T> AbrirAsync<T>(ModoUnidadTrabajo modo, Func<MySqlConnection, MySqlTransaction, ModoUnidadTrabajo, int, T> crear,
        CancellationToken ct) where T : UnidadMariaDb
    {
        if (!Enum.IsDefined(modo)) throw new ArgumentOutOfRangeException(nameof(modo));
        ct.ThrowIfCancellationRequested();
        var conexion = new MySqlConnection(cadenaConexion);
        T? unidad = null;
        try
        {
            await conexion.OpenAsync(ct);
            await PrepararSesionAsync(conexion, ct);
            var transaccion = modo == ModoUnidadTrabajo.Lectura
                ? await conexion.BeginTransactionAsync(IsolationLevel.RepeatableRead, isReadOnly: true, ct)
                : await conexion.BeginTransactionAsync(IsolationLevel.ReadCommitted, isReadOnly: false, ct);
            unidad = crear(conexion, transaccion, modo, configuracion.SegundosComando);
            if (modo == ModoUnidadTrabajo.Escritura) await unidad.TomarTurnoEscrituraAsync(ct);
            await unidad.ValidarAccesoAsync(ct);
            return unidad;
        }
        catch (Exception error)
        {
            if (unidad is not null) await unidad.DisposeAsync();
            else await conexion.DisposeAsync();
            if (error is ErrorAplicacionException) throw;
            if (error is OperationCanceledException && ct.IsCancellationRequested) throw;
            if (ct.IsCancellationRequested) throw new OperationCanceledException("La operación se canceló.", error, ct);
            if (error is MySqlException || TraduccionErrores.EsFalloTecnico(error)) throw Error.AlmacenNoDisponible(error);
            throw;
        }
    }

    private async Task PrepararSesionAsync(MySqlConnection conexion, CancellationToken ct)
    {
        await using var comando = new MySqlCommand(
            "SET SESSION sql_mode = @modo, SESSION time_zone = '+00:00', SESSION innodb_lock_wait_timeout = @espera", conexion)
        {
            CommandTimeout = configuracion.SegundosComando
        };
        comando.Parameters.Add(new MySqlParameter("@modo", MySqlDbType.VarChar) { Value = ModoSql });
        comando.Parameters.Add(new MySqlParameter("@espera", MySqlDbType.Int32) { Value = configuracion.SegundosEsperaBloqueo });
        await comando.ExecuteNonQueryAsync(ct);
    }
}
