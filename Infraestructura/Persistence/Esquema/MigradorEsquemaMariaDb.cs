using System.Security.Cryptography;
using System.Text;
using Clarisa.Infrastructure.Configuration;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Esquema;

// Se invoca explícitamente desde instalación o arranque del servidor, nunca desde constructores.
// MariaDB confirma cada DDL por separado: no hay reversión de una migración a medias. Cada sentencia es repetible y la versión
// se registra solo después de ejecutar todas y verificar el catálogo; tras un corte, la siguiente ejecución la completa.
public sealed class MigradorEsquemaMariaDb
{
    private const string CrearTablaVersiones = $"""
        CREATE TABLE {EsquemaEsperado.TablaVersiones} (
            version INT NOT NULL PRIMARY KEY,
            nombre VARCHAR(120) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
            huella VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
            aplicada_utc_ticks BIGINT NOT NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci COMMENT='{EsquemaEsperado.MarcaPropietario}'
        """;

    private readonly ConfiguracionMariaDb configuracion;
    private readonly string cadenaConexion;
    private readonly TimeProvider tiempo;

    public MigradorEsquemaMariaDb(ConfiguracionMariaDb configuracion, TimeProvider? tiempo = null)
    {
        ArgumentNullException.ThrowIfNull(configuracion);
        this.configuracion = configuracion;
        cadenaConexion = configuracion.CadenaConexion();
        this.tiempo = tiempo ?? TimeProvider.System;
    }

    public static int VersionEsperada => CatalogoMigraciones.VersionMaxima;

    public Task<EstadoEsquema> ConsultarAsync(CancellationToken ct) => ConConexionAsync(async conexion =>
        (await DiagnosticoEsquema.DiagnosticarAsync(conexion, configuracion.SegundosComando, ct)).Estado, ct);

    public Task<IReadOnlyList<int>> AplicarAsync(CancellationToken ct) => ConConexionAsync(async conexion =>
    {
        var cerrojo = NombreCerrojo(configuracion.Base);
        if (await EnteroAsync(conexion, "SELECT GET_LOCK(@nombre, @espera)", ct,
                ("@nombre", cerrojo), ("@espera", configuracion.SegundosEsperaBloqueo)) != 1)
            throw new EsquemaNoDisponibleException("Otro proceso está preparando la base de datos.", new TimeoutException());
        try
        {
            return await AplicarBajoCerrojoAsync(conexion, ct);
        }
        finally
        {
            if (conexion.State == System.Data.ConnectionState.Open)
                await EnteroAsync(conexion, "SELECT RELEASE_LOCK(@nombre)", CancellationToken.None, ("@nombre", cerrojo));
        }
    }, ct);

    public Task VerificarAsync(CancellationToken ct) => ConConexionAsync(async conexion =>
    {
        var diagnostico = await DiagnosticoEsquema.DiagnosticarAsync(conexion, configuracion.SegundosComando, ct);
        if (diagnostico.Estado != EstadoEsquema.AlDia)
            throw new MigracionRechazadaException($"El esquema no está al día (estado {diagnostico.Estado}).", diagnostico.Estado,
                diagnostico.Motivos);
        await ExigirCatalogoAsync(conexion, VersionEsperada, "El esquema no coincide con el esperado.", ct);
        return true;
    }, ct);

    private async Task<IReadOnlyList<int>> AplicarBajoCerrojoAsync(MySqlConnection conexion, CancellationToken ct)
    {
        // Se diagnostica de nuevo con el cerrojo tomado: otro migrador pudo haber avanzado mientras se esperaba.
        var diagnostico = await DiagnosticoEsquema.DiagnosticarAsync(conexion, configuracion.SegundosComando, ct);
        if (diagnostico.Estado is EstadoEsquema.MasNueva or EstadoEsquema.Ajena or EstadoEsquema.Alterada)
            throw new MigracionRechazadaException($"No se modifica una base en estado {diagnostico.Estado}.", diagnostico.Estado,
                diagnostico.Motivos);
        if (diagnostico.Estado == EstadoEsquema.Vacia) await EjecutarAsync(conexion, CrearTablaVersiones, ct);

        var aplicadas = new List<int>();
        foreach (var migracion in CatalogoMigraciones.Disponibles.Where(m => !diagnostico.Registradas.Contains(m.Version)))
        {
            await EjecutarMigracionAsync(conexion, migracion, ct);
            await ExigirCatalogoAsync(conexion, migracion.Version, $"La migración {migracion.Nombre} no dejó el esquema esperado.", ct);
            await EnteroAsync(conexion, $"""
                INSERT INTO {EsquemaEsperado.TablaVersiones} (version, nombre, huella, aplicada_utc_ticks)
                VALUES (@version, @nombre, @huella, @ticks)
                """, ct, ("@version", migracion.Version), ("@nombre", migracion.Nombre), ("@huella", migracion.Huella),
                ("@ticks", tiempo.GetUtcNow().UtcTicks));
            aplicadas.Add(migracion.Version);
        }
        await ExigirCatalogoAsync(conexion, VersionEsperada, "El esquema final no coincide con el esperado.", ct);
        return aplicadas;
    }

    // Un DDL que falla porque una tabla previa tiene otra forma no se resuelve reintentando: se informa como base alterada.
    private async Task EjecutarMigracionAsync(MySqlConnection conexion, Migracion migracion, CancellationToken ct)
    {
        foreach (var sentencia in migracion.Sentencias)
        {
            try
            {
                await EjecutarAsync(conexion, sentencia, ct);
            }
            catch (MySqlException error) when (!ct.IsCancellationRequested && conexion.State == System.Data.ConnectionState.Open)
            {
                var discrepancias = await VerificadorEsquema.VerificarAsync(conexion, migracion.Version, configuracion.SegundosComando, ct,
                    soloExistentes: true);
                if (discrepancias.Count == 0) throw;
                throw new MigracionRechazadaException($"La migración {migracion.Nombre} encontró tablas con otra forma.",
                    EstadoEsquema.Alterada, discrepancias, error);
            }
        }
    }

    private async Task ExigirCatalogoAsync(MySqlConnection conexion, int version, string mensaje, CancellationToken ct)
    {
        var discrepancias = await VerificadorEsquema.VerificarAsync(conexion, version, configuracion.SegundosComando, ct);
        if (discrepancias.Count > 0) throw new MigracionRechazadaException(mensaje, EstadoEsquema.Alterada, discrepancias);
    }

    private async Task<T> ConConexionAsync<T>(Func<MySqlConnection, Task<T>> accion, CancellationToken ct)
    {
        try
        {
            await using var conexion = new MySqlConnection(cadenaConexion);
            await conexion.OpenAsync(ct);
            await EnteroAsync(conexion, "SET SESSION sql_mode = @modo, SESSION time_zone = '+00:00'", ct,
                ("@modo", FabricaUnidadTrabajoMariaDb.ModoSql));
            return await accion(conexion);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (error is MySqlException || TraduccionErrores.EsFalloTecnico(error))
        {
            throw new EsquemaNoDisponibleException("No se pudo consultar o preparar la base de datos.", error);
        }
    }

    private Task EjecutarAsync(MySqlConnection conexion, string sql, CancellationToken ct) => EnteroAsync(conexion, sql, ct);

    private async Task<long> EnteroAsync(MySqlConnection conexion, string sql, CancellationToken ct,
        params (string Nombre, object Valor)[] parametros)
    {
        await using var comando = new MySqlCommand(sql, conexion) { CommandTimeout = configuracion.SegundosComando };
        foreach (var (nombre, valor) in parametros) comando.Parameters.Add(new MySqlParameter(nombre, valor));
        var resultado = await comando.ExecuteScalarAsync(ct);
        return resultado is null or DBNull ? 0 : Convert.ToInt64(resultado, System.Globalization.CultureInfo.InvariantCulture);
    }

    // Mismo nombre que en las versiones anteriores: un migrador antiguo y uno nuevo sobre la misma base deben excluirse entre sí.
    private static string NombreCerrojo(string baseDatos) =>
        "escolar_mig:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(baseDatos)))[..40];
}
