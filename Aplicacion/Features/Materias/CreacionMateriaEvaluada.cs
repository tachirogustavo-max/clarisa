using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Resultados;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Application.Features.ConfiguracionAcademica;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Materias;
public sealed record CrearMateriaEvaluada(Guid Id, string? Codigo, string? Nombre, Guid PlanId,
    Guid AnioLectivoId, string? Grado, ModalidadEvaluacion Modalidad);

public sealed class CreacionMateriaEvaluada(IFabricaUnidadTrabajo fabrica, IReloj reloj)
{
    public async Task<RegistroVersionado> CrearAsync(Actor actor, CrearMateriaEvaluada solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        if (datos.Modalidad is not (ModalidadEvaluacion.Numerica or ModalidadEvaluacion.Cualitativa or ModalidadEvaluacion.NumericaYCualitativa))
            throw Error.DatosInvalidos("Selecciona cómo se evaluará la materia.", "Modalidad");
        var propuesta = Error.DesdeDominio(() => new Materia(datos.Id, datos.Codigo, datos.Nombre, true, 1));
        var plan = Error.DesdeDominio(() => new PlanMateria(datos.PlanId, datos.AnioLectivoId, datos.Grado, propuesta.Id, datos.Modalidad, 1));
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        if (await unidad.AniosLectivos.ObtenerAsync(plan.AnioLectivoId, ct) is null)
            throw Error.ReferenciaInvalida("Selecciona un año escolar existente.", "AnioLectivoId");
        var materia = await unidad.Materias.ObtenerAsync(propuesta.Id, ct);
        if (materia is not null && !materia.MismoContenido(propuesta))
            throw Error.Duplicado("El identificador corresponde a una materia diferente. Consulta antes de repetir el registro.", "Id");
        var anterior = await unidad.PlanEstudios.BuscarAsync(plan.AnioLectivoId, plan.Grado, plan.MateriaId, ct);
        var porId = await unidad.PlanEstudios.ObtenerAsync(plan.Id, ct);
        if (porId is not null && (anterior is null || porId.Id != anterior.Id))
            throw Error.Duplicado("El identificador de evaluación pertenece a otra configuración.", "PlanId");
        if (anterior is not null)
        {
            if (materia is null || anterior.Id != plan.Id || anterior.Modalidad != plan.Modalidad)
                throw Error.Duplicado("La evaluación ya tiene otra configuración. Consúltala antes de volver a guardar.", "Modalidad");
            return new(materia.Id, materia.Version);
        }
        await CompatibilidadPlan.ExigirCapacidadAsync(unidad, plan.AnioLectivoId, ct);
        await CompatibilidadPlan.ExigirGradoAsync(unidad, plan.AnioLectivoId, plan.Grado, ct);
        await CompatibilidadPlan.ExigirAsync(unidad, plan.AnioLectivoId, plan.Grado, plan.MateriaId, plan.Modalidad, ct);
        if (materia is null)
        {
            if (await unidad.Materias.ExisteCodigoAsync(propuesta.Codigo, null, ct))
                throw Error.Duplicado("Ya existe una materia con ese código.", "Codigo");
            await unidad.Materias.AgregarAsync(propuesta, ct);
            await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Creacion, nameof(Materia), propuesta.Id, ct);
        }
        await unidad.PlanEstudios.AgregarAsync(plan, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Creacion, nameof(PlanMateria), plan.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new(propuesta.Id, materia?.Version ?? propuesta.Version);
    }
}
