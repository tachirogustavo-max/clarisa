using Clarisa.Application.Abstractions;
using Clarisa.Application.Common.Errores;

namespace Clarisa.Application.Features.Respaldos;

// Máquina de estados del trabajo. Solo usa servicios de vida única; nunca depende de una solicitud HTTP ya terminada.
public sealed class EjecucionRespaldos(IRegistroOperacionesRespaldo registro, IGeneradorCopias generador, IAlmacenCopias almacen,
    IClaveRespaldos clave, IVerificadorPermisoRespaldos permiso, CoordinacionRespaldos coordinacion, IReloj reloj)
{
    // Se ejecuta una vez al iniciar, antes de aceptar solicitudes. Las restauraciones las gestiona solo la herramienta local.
    public async Task<int> ReconciliarAsync(CancellationToken ct)
    {
        var revisadas = 0;
        foreach (var operacion in (await registro.ListarAsync(ct)).Where(o => o.Tipo == TipoOperacionRespaldo.Respaldo && o.Abierta))
        {
            await CerrarAsync(operacion, MensajesRespaldo.Interrumpida, ct);
            revisadas++;
        }
        almacen.DescartarTemporalesHuerfanos();
        coordinacion.MarcarLista();
        return revisadas;
    }

    // Tras un fallo inesperado (por ejemplo, no poder guardar el estado final después de publicar): la operación no queda abierta
    // hasta el próximo reinicio. Solo se da por completa si el archivo publicado coincide con lo verificado.
    public async Task RevisarAsync(Guid operacionId, CancellationToken ct)
    {
        if (await registro.ObtenerAsync(operacionId, ct) is { Tipo: TipoOperacionRespaldo.Respaldo, Abierta: true } operacion)
            await CerrarAsync(operacion, MensajesRespaldo.FalloInterno, ct);
    }

    private async Task CerrarAsync(OperacionRespaldo operacion, string motivoFallo, CancellationToken ct)
    {
        var publicada = operacion is { Fase: FaseOperacionRespaldo.Publicando, Bytes: { } bytes, Huella: { } huella }
            && await almacen.PublicadaCoincideAsync(operacion.Id, bytes, huella, ct);
        if (!publicada) almacen.DescartarTemporal(operacion.Id);
        await registro.GuardarAsync(publicada
            ? Cambiar(operacion, EstadoOperacionRespaldo.Completa, FaseOperacionRespaldo.Terminada, MensajesRespaldo.Reconciliada)
            : Cambiar(operacion, EstadoOperacionRespaldo.Fallida, FaseOperacionRespaldo.Terminada, motivoFallo), ct);
    }

    public async Task<IReadOnlyList<Guid>> PendientesAsync(CancellationToken ct) =>
        (await registro.ListarAsync(ct)).Where(o => o.Tipo == TipoOperacionRespaldo.Respaldo && o.Estado == EstadoOperacionRespaldo.Pendiente)
            .OrderBy(o => o.SolicitadaUtc).Select(o => o.Id).ToList();

    public async Task ProcesarAsync(Guid operacionId, CancellationToken ct)
    {
        var operacion = await registro.ObtenerAsync(operacionId, ct);
        if (operacion is not { Tipo: TipoOperacionRespaldo.Respaldo, Estado: EstadoOperacionRespaldo.Pendiente }) return;
        operacion = Cambiar(operacion, EstadoOperacionRespaldo.EnCurso, FaseOperacionRespaldo.Generando, MensajesRespaldo.Generando);
        await registro.GuardarAsync(operacion, ct);
        var publicada = false;
        try
        {
            var motivo = await MotivoRechazoAsync(operacion, ct);
            if (motivo is not null)
            {
                await registro.GuardarAsync(Cambiar(operacion, EstadoOperacionRespaldo.Fallida, FaseOperacionRespaldo.Terminada, motivo), ct);
                return;
            }
            var instante = reloj.Ahora;
            var copia = await generador.PrepararAsync(operacion.Id, instante, ct);
            operacion = Cambiar(operacion, EstadoOperacionRespaldo.EnCurso, FaseOperacionRespaldo.Publicando, MensajesRespaldo.Publicando)
                with { Nombre = NombreArchivo(operacion.Id, instante), Bytes = copia.Bytes, Huella = copia.Huella, InstanteCopia = instante };
            await registro.GuardarAsync(operacion, ct);
            await almacen.PublicarAsync(operacion.Id, CancellationToken.None);
            publicada = true;
            await registro.GuardarAsync(Cambiar(operacion, EstadoOperacionRespaldo.Completa, FaseOperacionRespaldo.Terminada,
                MensajesRespaldo.Completa), CancellationToken.None);
        }
        catch (Exception error) when (!publicada)
        {
            almacen.DescartarTemporal(operacion.Id);
            var mensaje = error switch
            {
                OperationCanceledException when ct.IsCancellationRequested => MensajesRespaldo.Detenida,
                ErrorAplicacionException conocido => conocido.Message,
                _ => MensajesRespaldo.FalloInterno
            };
            await registro.GuardarAsync(Cambiar(operacion, EstadoOperacionRespaldo.Fallida, FaseOperacionRespaldo.Terminada, mensaje),
                CancellationToken.None);
            if (error is not (OperationCanceledException or ErrorAplicacionException)) throw;
        }
    }

    public static string NombreArchivo(Guid id, DateTimeOffset instante) =>
        $"respaldo-{instante.UtcDateTime:yyyyMMdd-HHmmss}UTC-{id.ToString("N")[^8..]}.liceo-respaldo";

    private async Task<string?> MotivoRechazoAsync(OperacionRespaldo operacion, CancellationToken ct)
    {
        if (operacion.SolicitadaPor != CopiasAutomaticas.Sistema && !await permiso.EsAdministracionActivaAsync(operacion.SolicitadaPor, ct))
            return MensajesRespaldo.SinPermiso;
        if (!await clave.EstaPreparadaAsync(ct)) return MensajesRespaldo.SinClave;
        return almacen.HayEspacioSuficiente() ? null : MensajesRespaldo.SinEspacio;
    }

    private OperacionRespaldo Cambiar(OperacionRespaldo operacion, EstadoOperacionRespaldo estado, FaseOperacionRespaldo fase, string mensaje) =>
        operacion with { Estado = estado, Fase = fase, Mensaje = mensaje, ActualizadaUtc = reloj.Ahora };
}
