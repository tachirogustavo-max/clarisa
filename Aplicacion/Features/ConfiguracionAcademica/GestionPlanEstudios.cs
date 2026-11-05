using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Resultados;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Domain.Common;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.ConfiguracionAcademica;

public sealed class GestionPlanEstudios(IFabricaUnidadTrabajo fabrica, IReloj reloj)
{
    public const int MaximoListado = 500;
    private const string Entidad = nameof(PlanMateria);

    public async Task<RegistroVersionado> ConfigurarAsync(Actor actor, ConfigurarPlanMateria solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        var propuesto = Error.DesdeDominio(() => new PlanMateria(datos.Id, datos.AnioLectivoId, datos.Grado, datos.MateriaId,
            datos.Modalidad, version: 1));

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var existente = await unidad.PlanEstudios.ObtenerAsync(propuesto.Id, ct);
        if (existente is null)
        {
            if (datos.VersionEsperada != 0) throw Error.VersionObsoleta("VersionEsperada");
            await CompatibilidadPlan.ExigirCapacidadAsync(unidad, propuesto.AnioLectivoId, ct);
            await ExigirReferenciasAsync(unidad, propuesto, ct);
            await CompatibilidadPlan.ExigirGradoAsync(unidad, propuesto.AnioLectivoId, propuesto.Grado, ct);
            if (await unidad.PlanEstudios.BuscarAsync(propuesto.AnioLectivoId, propuesto.Grado, propuesto.MateriaId, ct) is not null)
                throw Error.Duplicado("Esta materia ya está en el plan del grado; modifique esa configuración.", nameof(PlanMateria.MateriaId));
            await CompatibilidadPlan.ExigirAsync(unidad, propuesto.AnioLectivoId, propuesto.Grado, propuesto.MateriaId,
                propuesto.Modalidad, ct);
            await unidad.PlanEstudios.AgregarAsync(propuesto, ct);
            await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Creacion, Entidad, propuesto.Id, ct);
            await unidad.ConfirmarAsync(ct);
            return new RegistroVersionado(propuesto.Id, propuesto.Version);
        }

        if (existente.AnioLectivoId != propuesto.AnioLectivoId || existente.MateriaId != propuesto.MateriaId
            || !Textos.Iguales(existente.Grado, propuesto.Grado))
            throw Error.Duplicado("El identificador pertenece a otra materia, grado o año del plan.", "Id");
        if (existente.Modalidad == propuesto.Modalidad) return new RegistroVersionado(existente.Id, existente.Version);
        Error.ExigirVersion(existente.Version, datos.VersionEsperada);
        await CompatibilidadPlan.ExigirAsync(unidad, existente.AnioLectivoId, existente.Grado, existente.MateriaId,
            propuesto.Modalidad, ct);
        existente.CambiarModalidad(propuesto.Modalidad);
        var version = await unidad.PlanEstudios.ActualizarAsync(existente, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Modificacion, Entidad, existente.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(existente.Id, version);
    }

    public async Task EliminarAsync(Actor actor, EliminarRegistro solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var plan = await unidad.PlanEstudios.ObtenerAsync(datos.Id, ct) ?? throw Error.NoEncontrado("La configuración del plan no existe.");
        Error.ExigirVersion(plan.Version, datos.VersionEsperada);
        await CompatibilidadPlan.ExigirAsync(unidad, plan.AnioLectivoId, plan.Grado, plan.MateriaId, nueva: null, ct);
        await unidad.PlanEstudios.EliminarAsync(plan.Id, plan.Version, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Eliminacion, Entidad, plan.Id, ct);
        await unidad.ConfirmarAsync(ct);
    }

    public async Task<IReadOnlyList<PlanMateriaDatos>> ListarAsync(Actor actor, Guid anioLectivoId, string? grado, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        if (anioLectivoId == Guid.Empty) throw Error.DatosInvalidos("Selecciona el año escolar.", "AnioLectivoId");
        var gradoValido = Error.DesdeDominio(() => Textos.Opcional(grado, "Grado", Seccion.MaximoGrado));
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var planes = await unidad.PlanEstudios.ListarAsync(anioLectivoId, gradoValido, MaximoListado, ct);
        return planes.Select(PlanMateriaDatos.Desde).ToList();
    }
    public async Task<IReadOnlyList<string>> GradosAsync(Actor actor, Guid anioLectivoId, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        if (anioLectivoId == Guid.Empty) throw Error.DatosInvalidos("Selecciona el año escolar.", "AnioLectivoId");
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        return await unidad.Secciones.GradosAsync(anioLectivoId, ct);
    }

    private static async Task ExigirReferenciasAsync(IUnidadTrabajo unidad, PlanMateria plan, CancellationToken ct)
    {
        if (await unidad.AniosLectivos.ObtenerAsync(plan.AnioLectivoId, ct) is null)
            throw Error.ReferenciaInvalida("El año escolar no existe.", nameof(PlanMateria.AnioLectivoId));
        if (await unidad.Materias.ObtenerAsync(plan.MateriaId, ct) is null)
            throw Error.ReferenciaInvalida("La materia no existe.", nameof(PlanMateria.MateriaId));
    }
}
