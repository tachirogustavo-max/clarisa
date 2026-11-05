using Clarisa.Application.Abstractions;
using Clarisa.Application.Common.Errores;

namespace Clarisa.Application.Features.Respaldos;

// Casos del operador local en el servidor detenido. No hay actor HTTP: la autoridad es el acceso al equipo y la credencial de la base.
public sealed class RecuperacionLocal(IRecuperadorCopias recuperador, IReferenciaBaseActiva referencia, IExclusionInstancia exclusion,
    IRegistroOperacionesRespaldo registro, IReloj reloj)
{
    public Task<ResumenCopia> ValidarAsync(string archivo, OrigenClave origen, CancellationToken ct) =>
        recuperador.ValidarAsync(ExigirArchivo(archivo), origen, ct);

    public async Task<(OperacionRespaldo Operacion, BaseRecuperada Base)> PrepararAsync(string archivo, OrigenClave origen,
        CredencialOperador operador, CancellationToken ct)
    {
        ExigirArchivo(archivo);
        using var turno = exclusion.Adquirir();
        var ahora = reloj.Ahora;
        var (operacion, _) = await registro.CrearSiNoExisteAsync(new(Guid.CreateVersion7(), TipoOperacionRespaldo.Restauracion,
            EstadoOperacionRespaldo.EnCurso, FaseOperacionRespaldo.Generando, "Recuperando la copia en una base nueva.", Guid.Empty,
            ahora, ahora), CancellationToken.None);
        try
        {
            var recuperada = await recuperador.RecuperarEnBaseNuevaAsync(archivo, origen, operador, ct);
            operacion = operacion with
            {
                Fase = FaseOperacionRespaldo.PendienteActivar, ActualizadaUtc = reloj.Ahora, Nombre = recuperada.Base,
                Huella = recuperada.Copia.HuellaContenido, Bytes = recuperada.Copia.TotalFilas, InstanteCopia = recuperada.Copia.CreadaUtc,
                Mensaje = $"Base nueva verificada ({recuperada.Copia.TotalFilas} registros). Pendiente de activar; "
                    + "el servidor sigue usando la base anterior."
            };
            await registro.GuardarAsync(operacion, CancellationToken.None);
            return (operacion, recuperada);
        }
        catch (Exception error)
        {
            var mensaje = error is ErrorAplicacionException conocido ? conocido.Message
                : "La recuperación no terminó. La base activa no cambió.";
            await registro.GuardarAsync(operacion with
            {
                Estado = EstadoOperacionRespaldo.Fallida, Fase = FaseOperacionRespaldo.Terminada, Mensaje = mensaje, ActualizadaUtc = reloj.Ahora
            }, CancellationToken.None);
            throw;
        }
    }

    public async Task<ReferenciaBase> ActivarAsync(Guid recuperacionId, CredencialOperador operador, CancellationToken ct)
    {
        using var turno = exclusion.Adquirir();
        var operacion = await registro.ObtenerAsync(recuperacionId, ct);
        if (operacion is not { Tipo: TipoOperacionRespaldo.Restauracion, Fase: FaseOperacionRespaldo.PendienteActivar,
                Estado: EstadoOperacionRespaldo.EnCurso, Nombre: { } baseNueva, Huella: { } huella })
            throw Error.EstadoNoPermitido("Esa recuperación no existe o no está pendiente de activar.");
        var actual = await referencia.LeerAsync(ct);
        if (actual.Base == baseNueva) throw Error.EstadoNoPermitido("Esa base ya es la activa.");

        await recuperador.VerificarContenidoAsync(baseNueva, huella, operador, ct);
        var activada = await referencia.ActivarAsync(baseNueva, operacion.Id, ct);
        await registro.GuardarAsync(operacion with
        {
            Estado = EstadoOperacionRespaldo.Completa, Fase = FaseOperacionRespaldo.Terminada, ActualizadaUtc = reloj.Ahora,
            Mensaje = $"Base recuperada activada para el próximo arranque del servidor. La base anterior ({actual.Base}) se conserva."
        }, CancellationToken.None);
        return activada;
    }

    public async Task<ReferenciaBase> RevertirAsync(CredencialOperador operador, CancellationToken ct)
    {
        using var turno = exclusion.Adquirir();
        var actual = await referencia.LeerAsync(ct);
        if (actual.Anterior is not { } anterior) throw Error.EstadoNoPermitido("No hay una base anterior registrada para revertir.");
        await recuperador.VerificarEsquemaAsync(anterior, operador, ct);
        var revertida = await referencia.ActivarAsync(anterior, null, ct);
        var ahora = reloj.Ahora;
        await registro.CrearSiNoExisteAsync(new(Guid.CreateVersion7(), TipoOperacionRespaldo.Restauracion, EstadoOperacionRespaldo.Completa,
            FaseOperacionRespaldo.Terminada, $"Se volvió a la base anterior ({anterior}) para el próximo arranque. La base {actual.Base} se conserva.",
            Guid.Empty, ahora, ahora, anterior), CancellationToken.None);
        return revertida;
    }

    public Task<ReferenciaBase> ConsultarActivaAsync(CancellationToken ct) => referencia.LeerAsync(ct);

    public async Task<IReadOnlyList<OperacionRespaldo>> RecuperacionesAsync(CancellationToken ct) =>
        (await registro.ListarAsync(ct)).Where(o => o.Tipo == TipoOperacionRespaldo.Restauracion)
            .OrderByDescending(o => o.SolicitadaUtc).ToList();

    private static string ExigirArchivo(string archivo) =>
        string.IsNullOrWhiteSpace(archivo) ? throw Error.DatosInvalidos("Indica el archivo de la copia.", "Archivo") : archivo;
}
