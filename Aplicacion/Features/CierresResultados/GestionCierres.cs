using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Application.Features.Boletines;
using Clarisa.Application.Features.Inscripciones;
using Clarisa.Domain.Common;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.CierresResultados;

public sealed class GestionCierres(IFabricaUnidadTrabajo fabrica, IReloj reloj)
{
    public const int MaximoCierresPorSeccion = 200;
    private const string Entidad = nameof(CierreResultados);

    public async Task<IReadOnlyList<CierreDatos>> ListarAsync(Actor actor, Guid seccionId, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        if (seccionId == Guid.Empty) throw Error.DatosInvalidos("Selecciona la sección.", nameof(CerrarResultados.SeccionId));
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        return (await unidad.Cierres.ListarAsync(seccionId, ct)).Select(CierreDatos.Desde).ToList();
    }

    public async Task<CierreDatos> CerrarAsync(Actor actor, CerrarResultados solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        if (datos.Id == Guid.Empty || datos.SeccionId == Guid.Empty)
            throw Error.DatosInvalidos("Selecciona la sección que se cierra.", nameof(CerrarResultados.SeccionId));

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var ahora = Instantes.Utc(reloj.Ahora);
        if (await unidad.Cierres.ObtenerAsync(datos.Id, ct) is { } repetido)
            return repetido.SeccionId == datos.SeccionId
                ? CierreDatos.Desde(repetido)
                : throw Error.Duplicado("Ya existe un cierre con este identificador para otra sección.", "Id");
        var seccion = await LecturaResultados.LeerSeccionAsync(unidad, datos.SeccionId, ct);
        if (seccion.Lapsos.Count != PoliticaAcademica.LapsosDelAnio)
            throw Error.EstadoNoPermitido("El año debe tener sus tres momentos registrados antes de cerrar resultados.");
        foreach (var lapso in seccion.Lapsos)
            if (await unidad.VentanasCarga.ObtenerPorLapsoAsync(lapso.Id, ct) is { } ventana && ventana.PermiteCargaEn(ahora))
                throw Error.EstadoNoPermitido($"La carga de notas del lapso «{lapso.Nombre}» sigue abierta. Ciérrala antes de cerrar resultados.");
        var politica = await unidad.Politicas.VigenteAsync(seccion.Anio.Id, ct);
        if (politica is not { Completa: true })
            throw Error.EstadoNoPermitido("Activa una política académica completa para el año antes de cerrar resultados.");
        if (await unidad.Cierres.VigenteAsync(seccion.Seccion.Id, ct) is not null)
            throw Error.EstadoNoPermitido("La sección ya tiene resultados cerrados. Para corregir, anula ese cierre con un motivo.");
        if ((await unidad.Cierres.ListarAsync(seccion.Seccion.Id, ct)).Count >= MaximoCierresPorSeccion)
            throw Error.EstadoNoPermitido("La sección alcanzó el límite de cierres registrados.");

        var inscritos = await Nominas.ActivosAsync(unidad, seccion.Seccion.Id, ct);
        var ids = inscritos.Select(i => i.Inscripcion.Id).ToList();
        var calificaciones = await unidad.Calificaciones.ListarPorInscripcionesAsync(ids, ct);
        var resultados = ids.SelectMany(id => LecturaResultados.Actuales(seccion, id, calificaciones, politica)).ToList();
        var numero = await unidad.Cierres.SiguienteNumeroAsync(seccion.Seccion.Id, ct);
        var cierre = Error.DesdeDominio(() => new CierreResultados(datos.Id, seccion.Anio.Id, seccion.Seccion.Id, numero, politica.Id,
            EstadoCierre.Vigente, new TrazaCierre(actor.UsuarioId, ahora), version: 1));
        await unidad.Cierres.AgregarAsync(cierre, resultados, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.CierreResultados, Entidad, cierre.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return CierreDatos.Desde(cierre);
    }

    public async Task<CierreDatos> AnularAsync(Actor actor, AnularCierre solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var cierre = await unidad.Cierres.ObtenerAsync(datos.Id, ct) ?? throw Error.NoEncontrado("El cierre no existe.");
        if (cierre.Estado == EstadoCierre.Anulado && cierre.Version == datos.VersionEsperada + 1) return CierreDatos.Desde(cierre);
        Error.ExigirVersion(cierre.Version, datos.VersionEsperada);
        Error.DesdeDominio(() => cierre.Anular(actor.UsuarioId, Instantes.Utc(reloj.Ahora), datos.Motivo));
        var version = await unidad.Cierres.ActualizarAsync(cierre, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.AnulacionCierre, Entidad, cierre.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return CierreDatos.Desde(cierre) with { Version = version };
    }

    // Se llama dentro de la unidad que escribe la nota: el cierre y el guardado quedan ordenados por el turno de escritura.
    internal static async Task ExigirSinCierreAsync(IUnidadTrabajo unidad, Guid seccionId, CancellationToken ct)
    {
        if (await unidad.Cierres.VigenteAsync(seccionId, ct) is not null)
            throw Error.EstadoNoPermitido("Los resultados de esta sección están cerrados. La dirección debe anular el cierre para corregirlos.");
    }
}
