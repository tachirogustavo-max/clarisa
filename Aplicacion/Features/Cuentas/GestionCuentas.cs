using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Application.Common.Consultas;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Resultados;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Cuentas;

// Las huellas de claves nuevas se calculan antes de abrir la unidad de escritura para no retener el turno durante ese cálculo.
public sealed class GestionCuentas(IFabricaUnidadSeguridad fabrica, IProtectorClaves protector, IListaClavesProhibidas prohibidas,
    IReloj reloj)
{
    public async Task<RegistroVersionado> CrearAsync(Actor actor, CrearCuenta solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        var perfil = PerfilesCuenta.Desde(datos.Perfil);
        var usuario = ReglasCuentas.UsuarioNormalizado(datos.Usuario);
        var clave = PoliticaClaves.ValidarNueva(datos.Clave, usuario, prohibidas);
        var nueva = Error.DesdeDominio(() =>
            new CuentaUsuario(datos.Id, usuario, datos.Nombre, perfil, datos.DocenteId, protector.Calcular(clave), activo: true, version: 1));

        CuentaUsuario? existente;
        await using (var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct))
        {
            existente = await unidad.Cuentas.ObtenerAsync(nueva.Id, ct);
            if (existente is null)
            {
                await ReglasCuentas.ExigirUsuarioLibreAsync(unidad, nueva.Usuario, null, ct);
                await ReglasCuentas.ExigirVinculoValidoAsync(unidad, nueva, ct);
                var ahora = ReglasCuentas.AhoraUtc(reloj);
                await unidad.Cuentas.AgregarAsync(nueva, ct);
                await ReglasCuentas.AuditarAsync(unidad, actor.UsuarioId, AccionAuditoriaCuenta.Creacion, nueva.Id, ahora, ct);
                await unidad.ConfirmarAsync(ct);
                return new RegistroVersionado(nueva.Id, nueva.Version);
            }
        }
        // Reintento con el mismo Id: la unidad ya se cerró sin cambios, así que verificar la clave no retiene el turno de escritura.
        return Reintentos.Resolver(existente.MismosDatos(nueva)
            && protector.Verificar(clave, existente.HuellaClave) != VerificacionClave.Incorrecta, existente.Id, existente.Version);
    }

    public async Task<RegistroVersionado> ModificarAsync(Actor actor, ModificarCuenta solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        var perfil = PerfilesCuenta.Desde(datos.Perfil);
        var usuario = ReglasCuentas.UsuarioNormalizado(datos.Usuario);
        var nuevaHuella = string.IsNullOrEmpty(datos.Clave)
            ? null
            : protector.Calcular(PoliticaClaves.ValidarNueva(datos.Clave, usuario, prohibidas));

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var cuenta = await unidad.Cuentas.ObtenerAsync(datos.Id, ct) ?? throw Error.NoEncontrado("La cuenta no existe.");
        Error.ExigirVersion(cuenta.Version, datos.VersionEsperada);
        var eraAdministracionActiva = EsAdministracionActiva(cuenta);
        var estabaActiva = cuenta.Activo;
        var cambiaAcceso = Error.DesdeDominio(() => cuenta.Modificar(usuario, datos.Nombre, perfil, datos.DocenteId, datos.Activo));
        if (eraAdministracionActiva && !EsAdministracionActiva(cuenta))
            await ExigirOtraAdministracionAsync(unidad, actor, cuenta, ct);
        await ReglasCuentas.ExigirUsuarioLibreAsync(unidad, cuenta.Usuario, cuenta.Id, ct);
        await ReglasCuentas.ExigirVinculoValidoAsync(unidad, cuenta, ct);
        if (nuevaHuella is not null) cuenta.CambiarHuellaClave(nuevaHuella);

        var ahora = ReglasCuentas.AhoraUtc(reloj);
        var version = await unidad.Cuentas.ActualizarAsync(cuenta, ct);
        if (cambiaAcceso || nuevaHuella is not null)
            await unidad.Sesiones.RevocarDeCuentaAsync(cuenta.Id, ahora, Motivo(cuenta, cambiaAcceso), ct);
        await ReglasCuentas.AuditarAsync(unidad, actor.UsuarioId, AccionAuditoriaCuenta.Modificacion, cuenta.Id, ahora, ct);
        if (nuevaHuella is not null)
            await ReglasCuentas.AuditarAsync(unidad, actor.UsuarioId, AccionAuditoriaCuenta.CambioClave, cuenta.Id, ahora, ct);
        if (estabaActiva && !cuenta.Activo)
            await ReglasCuentas.AuditarAsync(unidad, actor.UsuarioId, AccionAuditoriaCuenta.Desactivacion, cuenta.Id, ahora, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(cuenta.Id, version);
    }

    public async Task<CuentaDatos> ObtenerAsync(Actor actor, Guid id, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var cuenta = await unidad.Cuentas.ObtenerAsync(id, ct) ?? throw Error.NoEncontrado("La cuenta no existe.");
        return CuentaDatos.Desde(cuenta);
    }

    public async Task<Pagina<CuentaDatos>> BuscarAsync(Actor actor, FiltroTexto filtro, ConsultaPagina consulta, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var filtroValido = FiltroTexto.Validar(filtro);
        var pagina = ConsultaPagina.Validar(consulta);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var resultado = await unidad.Cuentas.BuscarAsync(filtroValido, pagina, ct);
        return resultado.Convertir(CuentaDatos.Desde);
    }

    private static bool EsAdministracionActiva(CuentaUsuario cuenta) => cuenta.Activo && cuenta.Perfil == PerfilCuenta.Administracion;

    // Se cuenta bajo el turno de escritura: dos administradores que se desactivan entre sí a la vez no dejan el sistema sin dirección.
    private static async Task ExigirOtraAdministracionAsync(IUnidadSeguridad unidad, Actor actor, CuentaUsuario cuenta,
        CancellationToken ct)
    {
        if (cuenta.Id == actor.UsuarioId)
            throw Error.EstadoNoPermitido("No puede desactivar su propia cuenta ni quitarse el perfil de administración.", "Activo");
        if (await unidad.Cuentas.ContarAdministradoresActivosAsync(ct) <= 1)
            throw Error.EstadoNoPermitido("Debe quedar al menos una cuenta de administración activa.", "Activo");
    }

    private static MotivoRevocacion Motivo(CuentaUsuario cuenta, bool cambiaAcceso) =>
        !cuenta.Activo ? MotivoRevocacion.Desactivacion
            : cambiaAcceso ? MotivoRevocacion.CambioPermisos
            : MotivoRevocacion.CambioClave;
}
