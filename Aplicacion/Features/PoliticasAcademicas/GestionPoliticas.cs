using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Resultados;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Domain.Common;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.PoliticasAcademicas;

// Solo la dirección crea, edita y activa. Activar no abre ninguna ventana de carga ni recalcula cierres ya hechos.
public sealed class GestionPoliticas(IFabricaUnidadTrabajo fabrica, IReloj reloj)
{
    public const int MaximoVersiones = 200;
    private const string Entidad = nameof(PoliticaAcademica);

    public async Task<IReadOnlyList<PoliticaAcademicaDatos>> ListarAsync(Actor actor, Guid anioLectivoId, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        if (anioLectivoId == Guid.Empty) throw Error.DatosInvalidos("Selecciona el año escolar.", nameof(CrearPolitica.AnioLectivoId));
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        return (await unidad.Politicas.ListarAsync(anioLectivoId, ct)).Select(PoliticaAcademicaDatos.Desde).ToList();
    }

    public async Task<PoliticaAcademicaDatos> CrearAsync(Actor actor, CrearPolitica solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        if (datos.Id == Guid.Empty || datos.AnioLectivoId == Guid.Empty)
            throw Error.DatosInvalidos("Selecciona el año escolar de la política.", nameof(CrearPolitica.AnioLectivoId));
        var ahora = Instantes.Utc(reloj.Ahora);

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        if (await unidad.Politicas.ObtenerAsync(datos.Id, ct) is { } existente)
        {
            // Reenvío de la misma creación tras perder la respuesta: se devuelve lo guardado si coincide.
            Reintentos.Resolver(existente.AnioLectivoId == datos.AnioLectivoId && existente.Estado == EstadoPolitica.Borrador, existente.Id,
                existente.Version);
            return PoliticaAcademicaDatos.Desde(existente);
        }
        if (await unidad.AniosLectivos.ObtenerAsync(datos.AnioLectivoId, ct) is null)
            throw Error.ReferenciaInvalida("El año escolar no existe.", nameof(CrearPolitica.AnioLectivoId));
        var versiones = await unidad.Politicas.ListarAsync(datos.AnioLectivoId, ct);
        if (versiones.Any(p => p.Estado == EstadoPolitica.Borrador))
            throw Error.EstadoNoPermitido("Ya hay un borrador para este año: edítalo o actívalo antes de crear otro.");
        if (versiones.Count >= MaximoVersiones) throw Error.EstadoNoPermitido("El año alcanzó el límite de versiones de política.");
        var reglas = datos.Reglas?.AReglas();
        if (datos.BasadaEn is { } baseId)
        {
            var origen = versiones.SingleOrDefault(p => p.Id == baseId)
                ?? throw Error.ReferenciaInvalida("La política de origen no es de este año.", nameof(CrearPolitica.BasadaEn));
            reglas ??= origen.Reglas;
        }
        var numero = await unidad.Politicas.SiguienteNumeroAsync(datos.AnioLectivoId, ct);
        var nueva = Error.DesdeDominio(() => new PoliticaAcademica(datos.Id, datos.AnioLectivoId, numero, EstadoPolitica.Borrador,
            reglas ?? new ReglasPolitica(null, null, null, null, null, null, null, null, null), datos.Motivo,
            new TrazaPolitica(actor.UsuarioId, ahora), version: 1));
        await unidad.Politicas.AgregarAsync(nueva, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.CreacionPolitica, Entidad, nueva.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return PoliticaAcademicaDatos.Desde(nueva);
    }

    public async Task<PoliticaAcademicaDatos> EditarAsync(Actor actor, EditarPolitica solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        var reglas = datos.Reglas ?? throw Error.DatosInvalidos("Faltan las reglas de la política.", nameof(EditarPolitica.Reglas));

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var politica = await unidad.Politicas.ObtenerAsync(datos.Id, ct) ?? throw Error.NoEncontrado("La política no existe.");
        if (politica.Estado == EstadoPolitica.Borrador && politica.Version != datos.VersionEsperada
            && politica.Reglas == reglas.AReglas() && politica.Motivo == datos.Motivo?.Trim())
            return PoliticaAcademicaDatos.Desde(politica);
        Error.ExigirVersion(politica.Version, datos.VersionEsperada);
        if (politica.Estado != EstadoPolitica.Borrador)
            throw Error.EstadoNoPermitido("Una política activada no se modifica: crea un borrador nuevo a partir de ella.");
        Error.DesdeDominio(() => politica.Editar(reglas.AReglas(), datos.Motivo));
        var version = await unidad.Politicas.ActualizarAsync(politica, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.CambioPolitica, Entidad, politica.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return PoliticaAcademicaDatos.Desde(politica) with { Version = version };
    }

    public async Task<PoliticaAcademicaDatos> ActivarAsync(Actor actor, ActivarPolitica solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        var ahora = Instantes.Utc(reloj.Ahora);

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var politica = await unidad.Politicas.ObtenerAsync(datos.Id, ct) ?? throw Error.NoEncontrado("La política no existe.");
        // Reintento de una activación ya confirmada: misma política vigente, sin efecto nuevo.
        if (politica.Estado == EstadoPolitica.Vigente && politica.Version == datos.VersionEsperada + 1)
            return PoliticaAcademicaDatos.Desde(politica);
        Error.ExigirVersion(politica.Version, datos.VersionEsperada);
        if (politica.Estado != EstadoPolitica.Borrador) throw Error.EstadoNoPermitido("Solo se activa una política en borrador.");
        if (politica.Reglas.DecimalesCaptura is { } decimales
            && await unidad.Calificaciones.ExistenConMasDecimalesAsync(politica.AnioLectivoId, decimales, ct))
            throw Error.EstadoNoPermitido($"Hay notas del año con más de {decimales} decimales: activar la política las dejaría fuera de su "
                + "precisión. Elige más decimales de captura o corrige esas notas primero.", "DecimalesCaptura");

        var anterior = await unidad.Politicas.VigenteAsync(politica.AnioLectivoId, ct);
        if (anterior is not null)
        {
            anterior.Reemplazar();
            await unidad.Politicas.ActualizarAsync(anterior, ct);
        }
        Error.DesdeDominio(() => politica.Activar(actor.UsuarioId, ahora, datos.Motivo));
        var version = await unidad.Politicas.ActualizarAsync(politica, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.ActivacionPolitica, Entidad, politica.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return PoliticaAcademicaDatos.Desde(politica) with { Version = version };
    }
}
