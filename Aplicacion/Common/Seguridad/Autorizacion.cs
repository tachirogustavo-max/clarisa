using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Errores;

namespace Clarisa.Application.Common.Seguridad;

public static class Autorizacion
{
    public static void ExigirAdministracion(Actor? actor)
    {
        if (actor is null || !actor.Tiene(Rol.Administracion))
            throw Error.AccesoDenegado("Esta operación corresponde a la administración.");
    }

    public static void ExigirPersonal(Actor? actor)
    {
        if (actor is null || !(actor.Tiene(Rol.Administracion) || DocenteVinculado(actor) is not null))
            throw Error.AccesoDenegado();
    }

    public static Guid ExigirDocente(Actor? actor) =>
        actor is null ? throw Error.AccesoDenegado() : DocenteVinculado(actor)
            ?? throw Error.AccesoDenegado("Esta consulta corresponde a una cuenta docente vinculada.");

    public static async Task<Guid> ExigirDocenteActivoAsync(Actor? actor, IUnidadTrabajo unidad, CancellationToken ct)
    {
        var docenteId = ExigirDocente(actor);
        var docente = await unidad.Docentes.ObtenerAsync(docenteId, ct);
        return docente is { Activo: true }
            ? docenteId
            : throw Error.AccesoDenegado("El registro docente vinculado a la cuenta no está activo.");
    }

    public static bool EsAdministracion(Actor actor) => actor.Tiene(Rol.Administracion);

    public static Guid? DocenteVinculado(Actor actor) => actor.Tiene(Rol.Docente) ? actor.DocenteId : null;
}
