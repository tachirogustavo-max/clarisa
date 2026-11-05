using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Clarisa.Desktop.Services.Catalogos;

internal sealed record RechazoCatalogo(string? Tipo, string? Mensaje, string? Campo)
{
    public static async Task<ResultadoRemoto<T>> LeerAsync<T>(HttpResponseMessage respuesta, CancellationToken ct)
    {
        var generico = (int)respuesta.StatusCode switch
        {
            401 => "Tu sesión terminó. Inicia sesión nuevamente.",
            403 => "Tu cuenta no tiene permiso para esta operación.",
            409 => "El registro cambió o ya existe. Revisa los datos antes de volver a guardar.",
            _ => "El servidor no pudo completar la operación. Tu edición se conserva."
        };
        if ((int)respuesta.StatusCode is not (400 or 409 or 422) || respuesta.Content.Headers.ContentType?.MediaType != "application/json")
            return new(default, generico);
        try
        {
            var rechazo = await respuesta.Content.ReadFromJsonAsync<RechazoCatalogo>(ct);
            if (rechazo?.Tipo is not ("DatosInvalidos" or "ReferenciaInvalida" or "Duplicado" or "VersionObsoleta" or "EstadoNoPermitido" or "EnUso" or "VentanaCerrada")
                || string.IsNullOrWhiteSpace(rechazo.Mensaje) || rechazo.Mensaje.Length > 500 || rechazo.Mensaje.Any(char.IsControl))
                return new(default, generico);
            var campo = rechazo.Campo is { Length: > 0 and <= 80 } valor && valor.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') ? rechazo.Campo : null;
            return new(default, rechazo.Mensaje, campo);
        }
        catch (JsonException) { return new(default, generico); }
    }
}
