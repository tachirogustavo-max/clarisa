using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Seguridad;

namespace Clarisa.Application.Features.LecturasAcademicas;

internal static class AccesoLecturas
{
    public static async Task<AlcanceAcademico> ResolverAsync(Actor actor, IUnidadLecturasAcademicas unidad, CancellationToken ct)
    {
        if (Autorizacion.EsAdministracion(actor)) return new(null);
        var docente = Autorizacion.ExigirDocente(actor);
        if (!await unidad.DocenteActivoAsync(docente, ct)) throw Error.AccesoDenegado("El registro docente no está activo.");
        return new(docente);
    }
}
