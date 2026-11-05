using Clarisa.Api.Http;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Features.Respaldos;
using Microsoft.Net.Http.Headers;

namespace Clarisa.Api.Endpoints;

// La estación solo aporta OperacionId o el Id de una copia conocida; el actor sale de la sesión validada.
public static class EndpointsRespaldos
{
    public static IEndpointRouteBuilder MapearRespaldos(this IEndpointRouteBuilder rutas)
    {
        var respaldos = rutas.MapGroup("/api/respaldos").RequireAuthorization(p => p.RequireRole("Administracion"));
        respaldos.MapGet("/", (HttpContext contexto, GestionRespaldos servicio, CancellationToken ct, int? pagina, int? tamanio) =>
            servicio.ListarAsync(contexto.ActorActual(), new(pagina ?? 1, tamanio ?? 25), ct));
        respaldos.MapPost("/", async (SolicitarRespaldo datos, HttpContext contexto, GestionRespaldos servicio, CancellationToken ct) =>
        {
            var aceptada = await servicio.SolicitarAsync(contexto.ActorActual(), datos, ct);
            return aceptada.Creada
                ? Results.Accepted($"/api/respaldos/operaciones/{aceptada.Operacion.Id}", aceptada.Operacion)
                : Results.Ok(aceptada.Operacion);
        });
        respaldos.MapGet("/operaciones/{id:guid}", (Guid id, HttpContext contexto, GestionRespaldos servicio, CancellationToken ct) =>
            servicio.ConsultarAsync(contexto.ActorActual(), id, ct));
        respaldos.MapGet("/{id:guid}/archivo", DescargarAsync);
        respaldos.MapGet("/restauracion", (HttpContext contexto, GestionRespaldos servicio) =>
            servicio.InstruirRestauracion(contexto.ActorActual()));
        // Ruta que la estación ya conoce: responde siempre con la instrucción y 409, nunca acepta un trabajo de restauración.
        respaldos.MapPost("/restaurar", (HttpContext contexto, GestionRespaldos servicio) =>
        {
            var instruccion = servicio.InstruirRestauracion(contexto.ActorActual());
            throw Error.EstadoNoPermitido(instruccion.Mensaje);
        });
        return rutas;
    }

    private static async Task DescargarAsync(Guid id, HttpContext contexto, GestionRespaldos servicio, CancellationToken ct)
    {
        var descarga = await servicio.PrepararDescargaAsync(contexto.ActorActual(), id, ct);
        var respuesta = contexto.Response;
        respuesta.ContentType = "application/octet-stream";
        respuesta.ContentLength = descarga.Bytes;
        respuesta.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = descarga.Nombre }.ToString();
        await descarga.EnviarAsync((bloque, cancelacion) => respuesta.Body.WriteAsync(bloque, cancelacion), ct);
    }
}
