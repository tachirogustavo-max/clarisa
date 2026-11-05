using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Clarisa.Desktop.Services;
public sealed class ClientePlanillas(HttpClient transporte, Uri servidor) : IClientePlanillas
{
    public async Task<RespuestaGuardado> GuardarAsync(ContextoPlanilla contexto, IReadOnlyList<NotaParaGuardar> notas, CancellationToken ct)
    {
        var destino = DireccionServidor.Validar(servidor.AbsoluteUri);
        if (destino.Error is not null)
            return Error(FalloGuardado.Rechazado, destino.Error);
        try
        {
            using var espera = CancellationTokenSource.CreateLinkedTokenSource(ct);
            espera.CancelAfter(TimeSpan.FromSeconds(20));
            using var solicitud = new HttpRequestMessage(HttpMethod.Post, new Uri(servidor, "api/calificaciones"))
                { Content = JsonContent.Create(new { contexto.SeccionId, contexto.MateriaId, contexto.LapsoId, notas }) };
            using var respuesta = await transporte.SendAsync(solicitud, HttpCompletionOption.ResponseHeadersRead, espera.Token);
            await respuesta.Content.LoadIntoBufferAsync(64 * 1024, espera.Token);
            if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
                return Error(FalloGuardado.Rechazado, "La sesión terminó. Tu edición se conserva; vuelve a iniciar sesión antes de guardar.");
            if (respuesta.StatusCode == HttpStatusCode.Forbidden)
                return Error(FalloGuardado.Rechazado, "Tu cuenta no tiene permiso para guardar esta planilla. La edición se conserva.");
            if (!respuesta.IsSuccessStatusCode)
            {
                var problema = await respuesta.Content.ReadFromJsonAsync<ProblemaPlanilla>(espera.Token);
                return problema?.Tipo switch
                {
                    "VentanaCerrada" => Error(FalloGuardado.VentanaCerrada, "La dirección cerró la carga de este momento. Tu edición se conserva sin enviar de nuevo."),
                    "VersionObsoleta" => Error(FalloGuardado.VersionObsoleta, "Otro usuario modificó notas de esta planilla. Revisa los cambios antes de volver a guardar."),
                    "ResultadoDesconocido" => Error(FalloGuardado.Conexion, "No sabemos si el servidor confirmó. Consulta la planilla antes de repetir el envío; tu edición se conserva."),
                    // Reglas de la planilla (letra fuera de la escala, resultados cerrados...): se muestra el motivo del servidor.
                    "DatosInvalidos" or "EstadoNoPermitido" or "ReferenciaInvalida" when problema.Mensaje is { Length: > 0 and <= 500 } motivo && !motivo.Any(char.IsControl)
                        => Error(FalloGuardado.Rechazado, motivo + " Tu edición se conserva."),
                    _ => Error(FalloGuardado.Rechazado, "El servidor no confirmó el guardado. Tu edición se conserva para revisarla.")};
            }

            var confirmacion = await respuesta.Content.ReadFromJsonAsync<ConfirmacionPlanilla>(espera.Token);
            if (confirmacion?.Notas is null || confirmacion.Notas.Any(nota => nota is null))
                return Error(FalloGuardado.Conexion, "No llegó una confirmación válida. Tu edición se conserva.");
            return new(confirmacion.Notas, FalloGuardado.Ninguno, null);
        }
        catch (OperationCanceledException)when (!ct.IsCancellationRequested)
        {
            return Error(FalloGuardado.Conexion, "Se agotó el tiempo de espera. No sabemos si el servidor confirmó; tu edición se conserva.");
        }
        catch (HttpRequestException)
        {
            return Error(FalloGuardado.Conexion, "Se perdió la conexión. No sabemos si el servidor confirmó; tu edición se conserva.");
        }
        catch (JsonException)
        {
            return Error(FalloGuardado.Conexion, "No se pudo interpretar la confirmación. Tu edición se conserva.");
        }
    }

    private static RespuestaGuardado Error(FalloGuardado fallo, string mensaje) => new([], fallo, mensaje);
    private sealed record ProblemaPlanilla(string? Tipo, string? Mensaje);
    private sealed record ConfirmacionPlanilla(IReadOnlyList<NotaConfirmada>? Notas);
}
