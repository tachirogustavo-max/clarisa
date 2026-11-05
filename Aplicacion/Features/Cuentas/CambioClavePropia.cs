using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Cuentas;

public sealed record SolicitudClavePropia(string? ClaveActual, string? ClaveNueva)
{
    public override string ToString() => "Cambio de contraseña protegido";
}

public sealed class CambioClavePropia(IFabricaUnidadSeguridad fabrica, IProtectorClaves protector,
    IListaClavesProhibidas prohibidas, LimitadorIntentos limitador, TurnoVerificacionClaves turnos, IReloj reloj)
{
    public async Task CambiarAsync(Actor actor, SolicitudClavePropia solicitud, string origen, CancellationToken ct)
    {
        Autorizacion.ExigirPersonal(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        var identificador = "clave:" + actor.UsuarioId.ToString("N");
        var procedencia = "clave:" + origen;
        if (limitador.Reservar(identificador, procedencia) is not null)
            throw Error.EstadoNoPermitido("Se hicieron demasiados intentos. Espera unos minutos antes de volver a intentar.", "ClaveActual");
        var decidido = false;
        try
        {
            CuentaUsuario leida;
            await using (var lectura = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct))
                leida = await ExigirCuentaAsync(lectura, actor, ct);
            var actual = PoliticaClaves.NormalizarEntrada(datos.ClaveActual);
            string huellaNueva;
            using (var turno = await turnos.EntrarAsync(ct))
            {
                if (turno is null) throw Error.EstadoNoPermitido("El servidor está ocupado. Espera un momento antes de volver a intentar.");
                if (actual is null || protector.Verificar(actual, leida.HuellaClave) == VerificacionClave.Incorrecta)
                {
                    limitador.RegistrarFallo(identificador, procedencia);
                    decidido = true;
                    throw Error.DatosInvalidos("La contraseña actual no es correcta.", "ClaveActual");
                }
                string nueva;
                try { nueva = PoliticaClaves.ValidarNueva(datos.ClaveNueva, leida.Usuario, prohibidas); }
                catch (ErrorAplicacionException error) when (error.Tipo == TipoError.DatosInvalidos)
                { throw Error.DatosInvalidos(error.Message, "ClaveNueva"); }
                if (string.Equals(actual, nueva, StringComparison.Ordinal))
                    throw Error.DatosInvalidos("Elige una contraseña diferente de la actual.", "ClaveNueva");
                huellaNueva = protector.Calcular(nueva);
            }
            ct.ThrowIfCancellationRequested();
            await using var escritura = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
            var cuenta = await ExigirCuentaAsync(escritura, actor, ct);
            if (cuenta.Version != leida.Version || cuenta.HuellaClave != leida.HuellaClave) throw Error.VersionObsoleta();
            cuenta.CambiarHuellaClave(huellaNueva);
            await escritura.Cuentas.ActualizarAsync(cuenta, ct);
            var ahora = ReglasCuentas.AhoraUtc(reloj);
            await escritura.Sesiones.RevocarDeCuentaAsync(cuenta.Id, ahora, MotivoRevocacion.CambioClave, ct);
            await ReglasCuentas.AuditarAsync(escritura, cuenta.Id, AccionAuditoriaCuenta.CambioClave, cuenta.Id, ahora, ct);
            await escritura.ConfirmarAsync(ct);
            limitador.RegistrarExito(identificador, procedencia);
            decidido = true;
        }
        finally { if (!decidido) limitador.Liberar(identificador, procedencia); }
    }

    private static async Task<CuentaUsuario> ExigirCuentaAsync(IUnidadSeguridad unidad, Actor actor, CancellationToken ct)
    {
        var cuenta = await unidad.Cuentas.ObtenerAsync(actor.UsuarioId, ct);
        if (cuenta is not { Activo: true }) throw Error.SesionNoValida();
        if (cuenta.Perfil == PerfilCuenta.Administracion && actor.Tiene(Rol.Administracion)) return cuenta;
        if (cuenta.Perfil != PerfilCuenta.Docente || !actor.Tiene(Rol.Docente) || cuenta.DocenteId != actor.DocenteId
            || cuenta.DocenteId is not { } docente || await unidad.Docentes.ObtenerAsync(docente, ct) is not { Activo: true })
            throw Error.SesionNoValida();
        return cuenta;
    }
}
