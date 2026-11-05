using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Resultados;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Inscripciones;

public sealed class GestionInscripciones(IFabricaUnidadTrabajo fabrica, IReloj reloj)
{
    private const string Entidad = nameof(Inscripcion);

    public async Task<RegistroVersionado> InscribirAsync(Actor actor, InscribirEstudiante solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var seccion = await unidad.Secciones.ObtenerAsync(datos.SeccionId, ct)
            ?? throw Error.ReferenciaInvalida("La sección seleccionada no existe.", nameof(InscribirEstudiante.SeccionId));
        var nueva = Error.DesdeDominio(() => new Inscripcion(datos.Id, datos.EstudianteId, seccion.Id, seccion.AnioLectivoId,
            datos.Fecha, EstadoInscripcion.Activa, fechaRetiro: null, version: 1));

        var existente = await unidad.Inscripciones.ObtenerAsync(nueva.Id, ct);
        if (existente is not null) return Reintentos.Resolver(existente.MismoContenido(nueva), existente.Id, existente.Version);

        var estudiante = await unidad.Estudiantes.ObtenerAsync(nueva.EstudianteId, ct)
            ?? throw Error.ReferenciaInvalida("El estudiante seleccionado no existe.", nameof(InscribirEstudiante.EstudianteId));
        if (!estudiante.Activo)
            throw Error.ReferenciaInvalida("El estudiante está inactivo.", nameof(InscribirEstudiante.EstudianteId));
        var anio = await unidad.AniosLectivos.ObtenerAsync(seccion.AnioLectivoId, ct)
            ?? throw Error.ReferenciaInvalida("El año lectivo de la sección no existe.", nameof(InscribirEstudiante.SeccionId));
        // La matrícula anticipada se admite: solo se exige que no sea posterior al cierre del año.
        if (nueva.Fecha > anio.Fechas.Fin)
            throw Error.DatosInvalidos("La fecha de inscripción no puede ser posterior al fin del año lectivo.", nameof(InscribirEstudiante.Fecha));
        if (await unidad.Inscripciones.ExisteActivaEnSeccionAsync(nueva.EstudianteId, nueva.SeccionId, ct))
            throw Error.Duplicado("El estudiante ya tiene una inscripción activa en esta sección.", nameof(InscribirEstudiante.EstudianteId));

        await unidad.Inscripciones.AgregarAsync(nueva, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Inscripcion, Entidad, nueva.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(nueva.Id, nueva.Version);
    }

    public async Task<RegistroVersionado> RetirarAsync(Actor actor, RetirarInscripcion solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var inscripcion = await unidad.Inscripciones.ObtenerAsync(datos.Id, ct) ?? throw Error.NoEncontrado("La inscripción no existe.");
        Error.ExigirVersion(inscripcion.Version, datos.VersionEsperada);
        if (inscripcion.Estado != EstadoInscripcion.Activa) throw Error.EstadoNoPermitido("La inscripción ya está retirada.");
        var anio = await unidad.AniosLectivos.ObtenerAsync(inscripcion.AnioLectivoId, ct)
            ?? throw Error.ReferenciaInvalida("El año lectivo de la inscripción no existe.");
        if (datos.Fecha > anio.Fechas.Fin)
            throw Error.DatosInvalidos("La fecha de retiro no puede ser posterior al fin del año lectivo.", nameof(RetirarInscripcion.Fecha));
        Error.DesdeDominio(() => inscripcion.Retirar(datos.Fecha));

        var version = await unidad.Inscripciones.ActualizarAsync(inscripcion, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Retiro, Entidad, inscripcion.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(inscripcion.Id, version);
    }

    public async Task<NominaSeccion> NominaAsync(Actor actor, Guid seccionId, CancellationToken ct)
    {
        Autorizacion.ExigirPersonal(actor);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        if (!Autorizacion.EsAdministracion(actor))
        {
            var docenteId = await Autorizacion.ExigirDocenteActivoAsync(actor, unidad, ct);
            if (!await unidad.AsignacionesDocentes.DocenteTieneSeccionAsync(docenteId, seccionId, ct))
                throw Error.AccesoDenegado("Solo puede consultar las secciones que tiene asignadas.");
        }
        if (await unidad.Secciones.ObtenerAsync(seccionId, ct) is null) throw Error.NoEncontrado("La sección no existe.");

        var inscritos = await Nominas.ActivosAsync(unidad, seccionId, ct);
        var nomina = inscritos.Select(inscrito => new EstudianteEnNomina(inscrito.Inscripcion.Id, inscrito.Estudiante.Id,
            inscrito.Estudiante.Identificacion, inscrito.Estudiante.Nombres, inscrito.Estudiante.Apellidos)).ToList();
        return new NominaSeccion(seccionId, nomina);
    }
}
