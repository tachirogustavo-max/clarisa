using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Domain.Common;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.VentanasCarga;

public sealed class GestionVentanasCarga(IFabricaUnidadTrabajo fabrica, IReloj reloj)
{
    public const int MaximoHistorial = 200;
    private const string Entidad = nameof(VentanaCarga);

    public async Task<VentanaCargaDatos> AbrirAsync(Actor actor, AbrirVentanaCarga solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        var hasta = datos.Hasta is { } limite ? Instantes.Utc(limite) : (DateTimeOffset?)null;

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var lapso = await unidad.Lapsos.ObtenerAsync(datos.LapsoId, ct) ?? throw Error.NoEncontrado("El momento no existe.");
        var ahora = Instantes.Utc(reloj.Ahora);
        var ventana = await unidad.VentanasCarga.ObtenerPorLapsoAsync(lapso.Id, ct);
        Error.ExigirVersion(ventana?.Version ?? 0, datos.VersionEsperada);
        // Ampliar el plazo de una ventana abierta no lo exige, para no cortar una carga ya en curso al actualizar.
        if (ventana?.PermiteCargaEn(ahora) != true && await unidad.Politicas.VigenteAsync(lapso.AnioLectivoId, ct) is not { Completa: true })
            throw Error.EstadoNoPermitido("Activa la política académica del año antes de abrir la carga de notas (Materias → Política del año).");

        AccionVentanaCarga accion;
        long version;
        if (ventana is null)
        {
            ventana = Error.DesdeDominio(() => VentanaCarga.AbrirNueva(Guid.CreateVersion7(ahora), lapso.Id, ahora, hasta));
            await unidad.VentanasCarga.AgregarAsync(ventana, ct);
            accion = AccionVentanaCarga.Apertura;
            version = ventana.Version;
        }
        else
        {
            var existente = ventana;
            accion = existente.PermiteCargaEn(ahora) ? AccionVentanaCarga.CambioPlazo : AccionVentanaCarga.Apertura;
            Error.DesdeDominio(() => existente.Abrir(ahora, hasta));
            version = await unidad.VentanasCarga.ActualizarAsync(existente, ct);
        }

        await RegistrarAsync(unidad, actor, ventana, accion, version, ahora, ct);
        await unidad.ConfirmarAsync(ct);
        return VentanaCargaDatos.Desde(lapso.Id, ventana, version, ahora);
    }

    public async Task<VentanaCargaDatos> CerrarAsync(Actor actor, CerrarVentanaCarga solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var ventana = await unidad.VentanasCarga.ObtenerPorLapsoAsync(datos.LapsoId, ct)
            ?? throw Error.NoEncontrado("Este momento no tiene una ventana de carga registrada.");
        Error.ExigirVersion(ventana.Version, datos.VersionEsperada);
        if (ventana.Estado != EstadoVentanaCarga.Abierta) throw Error.EstadoNoPermitido("La ventana de carga ya está cerrada.");
        var ahora = Instantes.Utc(reloj.Ahora);
        Error.DesdeDominio(() => ventana.Cerrar(ahora));

        var version = await unidad.VentanasCarga.ActualizarAsync(ventana, ct);
        await RegistrarAsync(unidad, actor, ventana, AccionVentanaCarga.Cierre, version, ahora, ct);
        await unidad.ConfirmarAsync(ct);
        return VentanaCargaDatos.Desde(ventana.LapsoId, ventana, version, ahora);
    }

    public async Task<VentanaCargaDatos> ConsultarAsync(Actor actor, Guid lapsoId, CancellationToken ct)
    {
        Autorizacion.ExigirPersonal(actor);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        if (await unidad.Lapsos.ObtenerAsync(lapsoId, ct) is null) throw Error.NoEncontrado("El momento no existe.");
        var ventana = await unidad.VentanasCarga.ObtenerPorLapsoAsync(lapsoId, ct);
        return VentanaCargaDatos.Desde(lapsoId, ventana, ventana?.Version ?? 0, Instantes.Utc(reloj.Ahora));
    }

    public async Task<IReadOnlyList<VentanaCargaDatos>> ListarPorAnioAsync(Actor actor, Guid anioLectivoId, CancellationToken ct)
    {
        Autorizacion.ExigirPersonal(actor);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        if (await unidad.AniosLectivos.ObtenerAsync(anioLectivoId, ct) is null) throw Error.NoEncontrado("El año lectivo no existe.");
        var lapsos = (await unidad.Lapsos.ListarPorAnioAsync(anioLectivoId, ct)).OrderBy(lapso => lapso.Orden).ToList();
        var ventanas = (await unidad.VentanasCarga.ListarPorLapsosAsync(lapsos.Select(lapso => lapso.Id).ToList(), ct))
            .ToDictionary(ventana => ventana.LapsoId);
        var ahora = Instantes.Utc(reloj.Ahora);
        return lapsos.Select(lapso =>
        {
            var ventana = ventanas.GetValueOrDefault(lapso.Id);
            return VentanaCargaDatos.Desde(lapso.Id, ventana, ventana?.Version ?? 0, ahora);
        }).ToList();
    }

    public async Task<IReadOnlyList<MovimientoVentanaCarga>> HistorialAsync(Actor actor, Guid lapsoId, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        if (await unidad.Lapsos.ObtenerAsync(lapsoId, ct) is null) throw Error.NoEncontrado("El momento no existe.");
        var movimientos = await unidad.VentanasCarga.ListarMovimientosAsync(lapsoId, MaximoHistorial, ct);
        return movimientos.OrderByDescending(movimiento => movimiento.OcurridoUtc).ThenByDescending(m => m.VersionResultante)
            .Take(MaximoHistorial).ToList();
    }

    private async Task RegistrarAsync(IUnidadTrabajo unidad, Actor actor, VentanaCarga ventana, AccionVentanaCarga accion,
        long version, DateTimeOffset ahora, CancellationToken ct)
    {
        await unidad.VentanasCarga.RegistrarMovimientoAsync(
            new MovimientoVentanaCarga(ventana.Id, ventana.LapsoId, accion, ventana.Hasta, version, actor.UsuarioId, ahora), ct);
        var accionAuditoria = accion switch
        {
            AccionVentanaCarga.Apertura => AccionAuditoria.AperturaVentanaCarga,
            AccionVentanaCarga.CambioPlazo => AccionAuditoria.CambioPlazoVentanaCarga,
            _ => AccionAuditoria.CierreVentanaCarga
        };
        await Auditar.RegistrarAsync(unidad, reloj, actor, accionAuditoria, Entidad, ventana.Id, ct);
    }
}
