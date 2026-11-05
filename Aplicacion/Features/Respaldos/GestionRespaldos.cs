using Clarisa.Application.Abstractions;
using Clarisa.Application.Common.Consultas;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Seguridad;

namespace Clarisa.Application.Features.Respaldos;

public sealed class GestionRespaldos(IRegistroOperacionesRespaldo registro, IAlmacenCopias almacen, IClaveRespaldos clave,
    IVerificadorPermisoRespaldos permiso, CoordinacionRespaldos coordinacion, IReloj reloj)
{
    public static readonly InstruccionRestauracion Restauracion = new(false,
        "La restauración no se ejecuta desde una estación. Se prepara y activa en el servidor con la herramienta de mantenimiento.",
        [
            "Descarga o identifica la copia que quieres recuperar.",
            "En el servidor, detén el servicio de la aplicación.",
            "Ejecuta «Clarisa.Mantenimiento preparar --archivo <copia>» con una cuenta de operador de la base de datos.",
            "Revisa el resumen: la copia se recupera en una base nueva y la base actual no se modifica.",
            "Ejecuta «Clarisa.Mantenimiento activar --recuperacion <Id>» para usarla en el próximo arranque.",
            "Inicia el servicio. Todas las sesiones anteriores quedan cerradas.",
            "Si necesitas volver atrás, detén el servicio y ejecuta «Clarisa.Mantenimiento revertir»."
        ]);

    public async Task<SolicitudAceptada> SolicitarAsync(Actor actor, SolicitarRespaldo? solicitud, CancellationToken ct)
    {
        var valida = Error.SolicitudRequerida(solicitud);
        if (valida.OperacionId == Guid.Empty) throw Error.DatosInvalidos("Indica el identificador de la operación.", "OperacionId");
        await ExigirAdministracionVigenteAsync(actor, ct);
        if (!coordinacion.Lista)
            throw Error.EstadoNoPermitido("El servicio de copias aún no está disponible: el servidor revisa operaciones anteriores o su registro "
                + "tiene un problema. Intenta más tarde o revisa el registro del servidor.");

        using var turno = await coordinacion.TomarAceptacionAsync(ct);
        var existente = await registro.ObtenerAsync(valida.OperacionId, ct);
        if (existente is not null) return new(Responder(existente), false);
        if (!await clave.EstaPreparadaAsync(ct)) throw Error.EstadoNoPermitido(MensajesRespaldo.SinClave);
        if ((await registro.ListarAsync(ct)).Any(o => o.Tipo == TipoOperacionRespaldo.Respaldo && o.Abierta))
            throw Error.EstadoNoPermitido("Ya hay una copia en preparación. Consulta su estado antes de solicitar otra.");
        if (!almacen.HayEspacioSuficiente()) throw Error.EstadoNoPermitido(MensajesRespaldo.SinEspacio);

        var ahora = reloj.Ahora;
        var nueva = new OperacionRespaldo(valida.OperacionId, TipoOperacionRespaldo.Respaldo, EstadoOperacionRespaldo.Pendiente,
            FaseOperacionRespaldo.Aceptada, MensajesRespaldo.Aceptada, actor.UsuarioId, ahora, ahora);
        // Una vez iniciada la escritura del registro, la cancelación del cliente ya no debe dejarla a medias.
        var (operacion, creada) = await registro.CrearSiNoExisteAsync(nueva, CancellationToken.None);
        if (creada) coordinacion.Avisar();
        return new(Responder(operacion), creada);
    }

    public async Task<OperacionRespaldoDatos> ConsultarAsync(Actor actor, Guid operacionId, CancellationToken ct)
    {
        await ExigirAdministracionVigenteAsync(actor, ct);
        var operacion = await registro.ObtenerAsync(operacionId, ct) ?? throw Error.NoEncontrado("No existe esa operación de respaldo.");
        return Responder(operacion);
    }

    public async Task<Pagina<RespaldoResumen>> ListarAsync(Actor actor, ConsultaPagina? consulta, CancellationToken ct)
    {
        var pagina = ConsultaPagina.Validar(consulta);
        await ExigirAdministracionVigenteAsync(actor, ct);
        var copias = (await registro.ListarAsync(ct)).Where(o => o.CopiaPublicada)
            .OrderByDescending(o => o.InstanteCopia).ThenByDescending(o => o.Id).ToList();
        var elementos = copias.Skip(pagina.Desplazamiento).Take(pagina.Tamanio)
            .Select(o => new RespaldoResumen(o.Id, o.Nombre!, o.InstanteCopia!.Value, o.Bytes!.Value, true)).ToList();
        return new(elementos, pagina.Numero, pagina.Tamanio, copias.Count);
    }

    public async Task<DescargaRespaldo> PrepararDescargaAsync(Actor actor, Guid respaldoId, CancellationToken ct)
    {
        await ExigirAdministracionVigenteAsync(actor, ct);
        var operacion = await registro.ObtenerAsync(respaldoId, ct);
        if (operacion is not { CopiaPublicada: true }) throw Error.NoEncontrado("La copia no existe o todavía no está disponible.");
        var (bytes, huella) = (operacion.Bytes!.Value, operacion.Huella!);
        return new(operacion.Id, operacion.Nombre!, bytes, (escritor, cancelacion) =>
            almacen.EnviarAsync(operacion.Id, bytes, huella, escritor, cancelacion));
    }

    public InstruccionRestauracion InstruirRestauracion(Actor actor)
    {
        Autorizacion.ExigirAdministracion(actor);
        return Restauracion;
    }

    public static OperacionRespaldoDatos Responder(OperacionRespaldo operacion) =>
        new(operacion.Id, operacion.Tipo.ToString(), operacion.Estado.ToString(), operacion.Mensaje);

    private async Task ExigirAdministracionVigenteAsync(Actor actor, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        if (!await permiso.EsAdministracionActivaAsync(actor.UsuarioId, ct))
            throw Error.AccesoDenegado("Esta operación corresponde a una cuenta de administración activa.");
    }
}
