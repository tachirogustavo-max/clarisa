using Clarisa.Api.Http;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Features.LecturasAcademicas;

namespace Clarisa.Api.Endpoints;

public static class EndpointsLecturas
{
    public static IEndpointRouteBuilder MapearLecturas(this IEndpointRouteBuilder rutas)
    {
        rutas.MapGet("/api/inscripciones", (HttpContext contexto, [AsParameters] ConsultaHttp consulta, LecturasAcademicas servicio, CancellationToken ct) =>
            servicio.InscripcionesAsync(contexto.ActorActual(), new(consulta.AnioLectivoId, null, consulta.Texto), consulta.AConsulta(), ct))
            .RequireAuthorization(p => p.RequireRole("Administracion"));
        rutas.MapGet("/api/asignaciones", (HttpContext contexto, [AsParameters] ConsultaHttp consulta, LecturasAcademicas servicio, CancellationToken ct) =>
            servicio.AsignacionesAsync(contexto.ActorActual(), new(consulta.AnioLectivoId, null, consulta.Texto), consulta.AConsulta(), ct)).RequireAuthorization();
        rutas.MapGet("/api/opciones/{recurso}", (string recurso, Guid? anioLectivoId, Guid? seccionId, int? limite,
            HttpContext contexto, LecturasAcademicas servicio, CancellationToken ct) =>
            servicio.OpcionesAsync(contexto.ActorActual(), Tipo(recurso), anioLectivoId, seccionId, limite ?? 200, ct)).RequireAuthorization();
        return rutas;
    }
    private static TipoCatalogoAcademico Tipo(string recurso) => recurso switch
    {
        "anios" => TipoCatalogoAcademico.Anios,
        "secciones" => TipoCatalogoAcademico.Secciones,
        "estudiantes" => TipoCatalogoAcademico.Estudiantes,
        "docentes" => TipoCatalogoAcademico.Docentes,
        "materias" => TipoCatalogoAcademico.Materias,
        "lapsos" => TipoCatalogoAcademico.Lapsos,
        _ => throw Error.NoEncontrado("La lista solicitada no existe.")
    };
}
