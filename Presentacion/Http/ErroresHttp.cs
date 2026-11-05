using Clarisa.Application.Common.Errores;
using Microsoft.AspNetCore.Diagnostics;

namespace Clarisa.Api.Http;

public sealed class ErroresHttp(ILogger<ErroresHttp> registro) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception error, CancellationToken ct)
    {
        if (error is OperationCanceledException && contexto.RequestAborted.IsCancellationRequested)
        {
            if (!contexto.Response.HasStarted) contexto.Response.StatusCode = 499;
            return true;
        }
        if (contexto.Response.HasStarted) return false;
        var conocido = error as ErrorAplicacionException;
        var codigo = conocido?.Tipo switch {
            TipoError.SesionNoValida => 401,
            TipoError.DatosInvalidos or TipoError.ReferenciaInvalida => 400,
            TipoError.AccesoDenegado => contexto.User.Identity?.IsAuthenticated == true ? 403 : 401,
            TipoError.NoEncontrado => 404,
            TipoError.Duplicado or TipoError.EnUso or TipoError.VersionObsoleta or TipoError.EstadoNoPermitido or TipoError.VentanaCerrada => 409,
            TipoError.AlmacenNoDisponible or TipoError.ResultadoDesconocido => 503,
            _ => error is BadHttpRequestException solicitud ? solicitud.StatusCode : 500
        };
        var mensaje = conocido?.Message ?? (codigo == 400 ? "La solicitud no tiene un formato válido." : "No se pudo completar la operación. Conserva tu edición y vuelve a consultar.");
        if (codigo >= 500) registro.LogError("Fallo {Tipo} en operación {Referencia}.", error.GetType().Name, contexto.TraceIdentifier);
        contexto.Response.StatusCode = codigo;
        await contexto.Response.WriteAsJsonAsync(new { Tipo = conocido?.Tipo.ToString() ?? "SolicitudNoCompletada", Mensaje = mensaje,
            Campo = conocido?.Campo, Referencia = contexto.TraceIdentifier }, cancellationToken: ct);
        return true;
    }
}
