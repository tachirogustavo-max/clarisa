using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Resultados;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.AsignacionesDocentes;

public sealed class GestionAsignacionesDocentes(IFabricaUnidadTrabajo fabrica, IReloj reloj)
{
    private const string Entidad = nameof(AsignacionDocente);

    public async Task<RegistroVersionado> AsignarAsync(Actor actor, AsignarDocente solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        var nueva = Error.DesdeDominio(() =>
            new AsignacionDocente(datos.Id, datos.SeccionId, datos.MateriaId, datos.DocenteId, version: 1));

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var existente = await unidad.AsignacionesDocentes.ObtenerAsync(nueva.Id, ct);
        if (existente is not null) return Reintentos.Resolver(existente.MismoContenido(nueva), existente.Id, existente.Version);

        if (await unidad.Secciones.ObtenerAsync(nueva.SeccionId, ct) is null)
            throw Error.ReferenciaInvalida("La sección seleccionada no existe.", nameof(AsignarDocente.SeccionId));
        var materia = await unidad.Materias.ObtenerAsync(nueva.MateriaId, ct);
        if (materia is not { Activa: true })
            throw Error.ReferenciaInvalida("La materia seleccionada no existe o está inactiva.", nameof(AsignarDocente.MateriaId));
        await ExigirDocenteActivoAsync(unidad, nueva.DocenteId, ct);
        if (await unidad.AsignacionesDocentes.ExisteAsync(nueva.SeccionId, nueva.MateriaId, nueva.DocenteId, ct))
            throw Error.Duplicado("Ese docente ya tiene asignada esta materia en la sección.", nameof(AsignarDocente.DocenteId));

        await unidad.AsignacionesDocentes.AgregarAsync(nueva, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Creacion, Entidad, nueva.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(nueva.Id, nueva.Version);
    }

    public async Task<RegistroVersionado> CambiarDocenteAsync(Actor actor, CambiarDocente solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var asignacion = await unidad.AsignacionesDocentes.ObtenerAsync(datos.Id, ct)
            ?? throw Error.NoEncontrado("La asignación no existe.");
        Error.ExigirVersion(asignacion.Version, datos.VersionEsperada);
        if (asignacion.DocenteId == datos.DocenteId)
            throw Error.DatosInvalidos("Selecciona un docente distinto al actual.", nameof(CambiarDocente.DocenteId));
        Error.DesdeDominio(() => asignacion.CambiarDocente(datos.DocenteId));
        await ExigirDocenteActivoAsync(unidad, asignacion.DocenteId, ct);
        if (await unidad.AsignacionesDocentes.ExisteAsync(asignacion.SeccionId, asignacion.MateriaId, asignacion.DocenteId, ct))
            throw Error.Duplicado("Ese docente ya tiene asignada esta materia en la sección.", nameof(CambiarDocente.DocenteId));

        var version = await unidad.AsignacionesDocentes.ActualizarAsync(asignacion, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.CambioDocente, Entidad, asignacion.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(asignacion.Id, version);
    }

    public async Task RetirarAsync(Actor actor, EliminarRegistro solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var asignacion = await unidad.AsignacionesDocentes.ObtenerAsync(datos.Id, ct)
            ?? throw Error.NoEncontrado("La asignación no existe.");
        Error.ExigirVersion(asignacion.Version, datos.VersionEsperada);
        // Las notas pertenecen a inscripción, materia y lapso, no al docente; solo se protege que la materia siga en la sección.
        var mismaMateria = (await unidad.AsignacionesDocentes.ListarPorSeccionAsync(asignacion.SeccionId, ct))
            .Count(otra => otra.MateriaId == asignacion.MateriaId);
        if (mismaMateria <= 1 && await unidad.Calificaciones.ExistenEnSeccionMateriaAsync(asignacion.SeccionId, asignacion.MateriaId, ct))
            throw Error.EnUso("Es la única asignación de esta materia en la sección y ya hay notas registradas. Asigne otro docente antes de retirarla.");

        await unidad.AsignacionesDocentes.EliminarAsync(asignacion.Id, asignacion.Version, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Eliminacion, Entidad, asignacion.Id, ct);
        await unidad.ConfirmarAsync(ct);
    }

    public async Task<IReadOnlyList<AsignacionDocenteDatos>> ListarPorSeccionAsync(Actor actor, Guid seccionId, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        if (await unidad.Secciones.ObtenerAsync(seccionId, ct) is null) throw Error.NoEncontrado("La sección no existe.");
        var asignaciones = await unidad.AsignacionesDocentes.ListarPorSeccionAsync(seccionId, ct);
        return Ordenar(asignaciones);
    }

    public async Task<IReadOnlyList<AsignacionDocenteDatos>> MisAsignacionesAsync(Actor actor, CancellationToken ct)
    {
        Autorizacion.ExigirDocente(actor);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var docenteId = await Autorizacion.ExigirDocenteActivoAsync(actor, unidad, ct);
        var asignaciones = await unidad.AsignacionesDocentes.ListarPorDocenteAsync(docenteId, ct);
        return Ordenar(asignaciones);
    }

    private static List<AsignacionDocenteDatos> Ordenar(IEnumerable<AsignacionDocente> asignaciones) =>
        asignaciones.OrderBy(a => a.SeccionId).ThenBy(a => a.MateriaId).ThenBy(a => a.DocenteId).Select(AsignacionDocenteDatos.Desde).ToList();

    private static async Task ExigirDocenteActivoAsync(IUnidadTrabajo unidad, Guid docenteId, CancellationToken ct)
    {
        var docente = await unidad.Docentes.ObtenerAsync(docenteId, ct);
        if (docente is not { Activo: true })
            throw Error.ReferenciaInvalida("El docente seleccionado no existe o está inactivo.", nameof(AsignarDocente.DocenteId));
    }
}
