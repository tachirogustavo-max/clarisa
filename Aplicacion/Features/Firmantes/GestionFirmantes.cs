using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Resultados;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Domain.Common;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Firmantes;

// El cambio vale para los boletines que se generen desde ese momento; los PDF ya guardados no se tocan.
public sealed class GestionFirmantes(IFabricaUnidadTrabajo fabrica, IFirmantesConfigurados configurados, IReloj reloj)
{
    public async Task<FirmantesDatos> ConsultarAsync(Actor actor, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        return Datos(await unidad.Firmantes.VigentesAsync(ct));
    }

    public async Task<FirmantesDatos> CambiarAsync(Actor actor, CambiarFirmantes solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        if (datos.Id == Guid.Empty) throw Error.DatosInvalidos("Falta el identificador del cambio.", nameof(CambiarFirmantes.Id));

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        if (await unidad.Firmantes.ObtenerAsync(datos.Id, ct) is { } existente)
        {
            // Reenvío del mismo cambio tras perder la respuesta.
            Reintentos.Resolver(existente.Numero == datos.NumeroEsperado + 1, existente.Id, existente.Numero);
            return FirmantesDatos.Desde(existente);
        }
        var actual = await unidad.Firmantes.VigentesAsync(ct);
        Error.ExigirVersion(actual?.Numero ?? 0, datos.NumeroEsperado);
        var nuevos = Error.DesdeDominio(() => new FirmantesBoletin(datos.Id, datos.NumeroEsperado + 1, datos.Director,
            datos.CoordinadorEvaluacion, datos.CoordinadorEstudios, datos.Motivo, actor.UsuarioId, Instantes.Utc(reloj.Ahora)));
        await unidad.Firmantes.AgregarAsync(nuevos, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.CambioFirmantes, nameof(FirmantesBoletin), nuevos.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return FirmantesDatos.Desde(nuevos);
    }

    internal async Task<NombresFirmantes> NombresAsync(CancellationToken ct)
    {
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        return Datos(await unidad.Firmantes.VigentesAsync(ct)).Nombres;
    }

    private FirmantesDatos Datos(FirmantesBoletin? vigentes) =>
        vigentes is null ? new(configurados.Predeterminados, 0, null, null) : FirmantesDatos.Desde(vigentes);
}
