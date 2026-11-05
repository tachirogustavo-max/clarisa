using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Domain.Entities;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

internal sealed class RepositorioVentanasCargaMariaDb(SesionMariaDb sesion) : IRepositorioVentanasCarga
{
    public const int MaximoMovimientos = 1000;

    private const string Columnas = """
        SELECT id, lapso_id, estado, abierta_desde_utc_ticks, hasta_utc_ticks, cerrada_en_utc_ticks, version FROM ventanas_carga
        """;

    // En escritura, cierre y guardado bloquean la misma fila hasta confirmar (además del turno de escritura de la unidad).
    // En READ COMMITTED la lectura con bloqueo devuelve siempre la última versión confirmada.
    public Task<VentanaCarga?> ObtenerPorLapsoAsync(Guid lapsoId, CancellationToken ct) =>
        sesion.PrimeroAsync(Columnas + " WHERE lapso_id = @lapso", Convertir, bloquear: sesion.Modo == ModoUnidadTrabajo.Escritura, ct,
            Parametros.Id("@lapso", lapsoId));

    public Task<IReadOnlyList<VentanaCarga>> ListarPorLapsosAsync(IReadOnlyCollection<Guid> lapsoIds, CancellationToken ct) =>
        ConsultaPorIds.ListarAsync(sesion, Columnas + " WHERE lapso_id IN ({0})", lapsoIds, Convertir, ct);

    public Task AgregarAsync(VentanaCarga ventana, CancellationToken ct) =>
        sesion.EjecutarAsync("""
            INSERT INTO ventanas_carga (id, lapso_id, estado, abierta_desde_utc_ticks, hasta_utc_ticks, cerrada_en_utc_ticks, version)
            VALUES (@id, @lapso, @estado, @desde, @hasta, @cerrada, @version)
            """, ct, Datos(ventana));

    public async Task<long> ActualizarAsync(VentanaCarga ventana, CancellationToken ct)
    {
        var filas = await sesion.EjecutarAsync("""
            UPDATE ventanas_carga SET estado = @estado, abierta_desde_utc_ticks = @desde, hasta_utc_ticks = @hasta,
                cerrada_en_utc_ticks = @cerrada, version = version + 1
            WHERE id = @id AND lapso_id = @lapso AND version = @version
            """, ct, Datos(ventana));
        await sesion.ExigirUnaFilaAsync(filas, "ventanas_carga", ventana.Id, ct);
        return ventana.Version + 1;
    }

    public Task RegistrarMovimientoAsync(MovimientoVentanaCarga movimiento, CancellationToken ct) =>
        sesion.EjecutarAsync("""
            INSERT INTO movimientos_ventana_carga (ventana_id, lapso_id, accion, hasta_utc_ticks, version_resultante, actor_usuario_id,
                ocurrido_utc_ticks)
            VALUES (@ventana, @lapso, @accion, @hasta, @version, @actor, @ocurrido)
            """, ct,
            Parametros.Id("@ventana", movimiento.VentanaId),
            Parametros.Id("@lapso", movimiento.LapsoId),
            Parametros.Entero("@accion", (int)movimiento.Accion),
            Parametros.InstanteOpcional("@hasta", movimiento.Hasta),
            Parametros.Largo("@version", movimiento.VersionResultante),
            Parametros.Id("@actor", movimiento.ActorUsuarioId),
            Parametros.Instante("@ocurrido", movimiento.OcurridoUtc));

    public Task<IReadOnlyList<MovimientoVentanaCarga>> ListarMovimientosAsync(Guid lapsoId, int maximo, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximo, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximo, MaximoMovimientos);
        return LeerMovimientosAsync(lapsoId, maximo, ct);
    }

    private async Task<IReadOnlyList<MovimientoVentanaCarga>> LeerMovimientosAsync(Guid lapsoId, int maximo, CancellationToken ct) =>
        await sesion.PaginaAsync("""
            SELECT ventana_id, lapso_id, accion, hasta_utc_ticks, version_resultante, actor_usuario_id, ocurrido_utc_ticks
            FROM movimientos_ventana_carga WHERE lapso_id = @lapso
            ORDER BY ocurrido_utc_ticks DESC, version_resultante DESC, id DESC LIMIT @maximo
            """, fila => new MovimientoVentanaCarga(fila.Id(0), fila.Id(1), fila.Enumerado<AccionVentanaCarga>(2), fila.InstanteOpcional(3),
                fila.GetInt64(4), fila.Id(5), fila.Instante(6)),
            maximo, ct, Parametros.Id("@lapso", lapsoId), Parametros.Entero("@maximo", maximo));

    private static Parametro[] Datos(VentanaCarga ventana) =>
    [
        Parametros.Id("@id", ventana.Id),
        Parametros.Id("@lapso", ventana.LapsoId),
        Parametros.Entero("@estado", (int)ventana.Estado),
        Parametros.Instante("@desde", ventana.AbiertaDesde),
        Parametros.InstanteOpcional("@hasta", ventana.Hasta),
        Parametros.InstanteOpcional("@cerrada", ventana.CerradaEn),
        Parametros.Largo("@version", ventana.Version)
    ];

    private static VentanaCarga Convertir(MySqlDataReader fila) =>
        new(fila.Id(0), fila.Id(1), fila.Enumerado<EstadoVentanaCarga>(2), fila.Instante(3), fila.InstanteOpcional(4),
            fila.InstanteOpcional(5), fila.GetInt64(6));
}
