using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Resultados;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.ConfiguracionAcademica;

// Las letras no traen significado ni equivalencia numérica: solo existen las que la dirección registra para el año.
public sealed class GestionEscalaCualitativa(IFabricaUnidadTrabajo fabrica, IReloj reloj)
{
    private const string Entidad = nameof(LetraCualitativa);

    public async Task<RegistroVersionado> AgregarAsync(Actor actor, AgregarLetraCualitativa solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        var nueva = Error.DesdeDominio(() => new LetraCualitativa(datos.Id, datos.AnioLectivoId, datos.Letra, datos.Descripcion, version: 1));

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var existente = await unidad.EscalaCualitativa.ObtenerAsync(nueva.Id, ct);
        if (existente is not null) return Reintentos.Resolver(existente.MismoContenido(nueva), existente.Id, existente.Version);
        if (await unidad.AniosLectivos.ObtenerAsync(nueva.AnioLectivoId, ct) is null)
            throw Error.ReferenciaInvalida("El año escolar no existe.", nameof(LetraCualitativa.AnioLectivoId));
        var escala = await unidad.EscalaCualitativa.ListarAsync(nueva.AnioLectivoId, ct);
        if (escala.Any(letra => letra.Letra == nueva.Letra))
            throw Error.Duplicado("Esa letra ya está en la escala del año.", nameof(LetraCualitativa.Letra));
        if (escala.Count >= 100) throw Error.EstadoNoPermitido("La escala del año alcanzó el límite de letras. Revisa su configuración.");

        await unidad.EscalaCualitativa.AgregarAsync(nueva, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Creacion, Entidad, nueva.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(nueva.Id, nueva.Version);
    }

    public async Task EliminarAsync(Actor actor, EliminarRegistro solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var letra = await unidad.EscalaCualitativa.ObtenerAsync(datos.Id, ct) ?? throw Error.NoEncontrado("La letra no existe.");
        Error.ExigirVersion(letra.Version, datos.VersionEsperada);
        if (await unidad.EscalaCualitativa.EnUsoAsync(letra.AnioLectivoId, letra.Letra, ct))
            throw Error.EnUso("La letra tiene resultados o historial registrados; no se elimina.");
        await unidad.EscalaCualitativa.EliminarAsync(letra.Id, letra.Version, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Eliminacion, Entidad, letra.Id, ct);
        await unidad.ConfirmarAsync(ct);
    }

    public async Task<IReadOnlyList<LetraCualitativaDatos>> ListarAsync(Actor actor, Guid anioLectivoId, CancellationToken ct)
    {
        Autorizacion.ExigirPersonal(actor);
        if (anioLectivoId == Guid.Empty) throw Error.DatosInvalidos("Selecciona el año escolar.", "AnioLectivoId");
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        return (await unidad.EscalaCualitativa.ListarAsync(anioLectivoId, ct)).Select(LetraCualitativaDatos.Desde).ToList();
    }
}
