using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Domain.Common;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Cuentas;

// Toda respuesta negativa al iniciar sesión es la misma, exista o no la cuenta. El cálculo costoso de la clave se hace fuera
// de la unidad de escritura; después se relee la cuenta y se exige la misma versión antes de crear la sesión.
public sealed class AccesoCuentas(IFabricaUnidadSeguridad fabrica, IProtectorClaves protector, IGeneradorTokensSesion tokens,
    LimitadorIntentos limitador, TurnoVerificacionClaves turnoClaves, IReloj reloj, OpcionesSesiones opciones,
    HuellaVerificacionSenuelo senuelo)
{
    public static readonly TimeSpan EsperaSinTurno = TimeSpan.FromSeconds(5);

    public async Task<ResultadoInicioSesion> IniciarAsync(IniciarSesion? solicitud, CancellationToken ct)
    {
        var usuarioValido = NombresUsuario.IntentarNormalizar(solicitud?.Usuario, out var usuario);
        var claveLimite = usuarioValido ? usuario : "?" + (solicitud?.Usuario ?? "");
        var origen = solicitud?.Origen ?? "";
        if (limitador.Reservar(claveLimite, origen) is { } espera)
            return new ResultadoInicioSesion(EstadoInicioSesion.Bloqueada, null, espera);

        var decidido = false;
        try
        {
            var resultado = await VerificarYCrearAsync(usuarioValido ? usuario : null, solicitud?.Clave, ct);
            if (resultado.Estado == EstadoInicioSesion.Iniciada) limitador.RegistrarExito(claveLimite, origen);
            else if (resultado.Estado == EstadoInicioSesion.Rechazada) limitador.RegistrarFallo(claveLimite, origen);
            decidido = resultado.Estado != EstadoInicioSesion.Bloqueada;
            return resultado;
        }
        finally
        {
            if (!decidido) limitador.Liberar(claveLimite, origen);
        }
    }

    private async Task<ResultadoInicioSesion> VerificarYCrearAsync(string? usuario, string? claveRecibida, CancellationToken ct)
    {
        var clave = PoliticaClaves.NormalizarEntrada(claveRecibida);
        var (cuenta, docenteActivo) = usuario is not null && clave is not null ? await LeerCuentaAsync(usuario, ct) : (null, false);
        using var turno = await turnoClaves.EntrarAsync(ct);
        if (turno is null) return new ResultadoInicioSesion(EstadoInicioSesion.Bloqueada, null, EsperaSinTurno);
        var verificacion = protector.Verificar(clave ?? "", cuenta?.HuellaClave ?? senuelo.Valor);
        if (cuenta is null || clave is null || verificacion == VerificacionClave.Incorrecta || !PuedeEntrar(cuenta, docenteActivo))
            return ResultadoInicioSesion.Rechazada;
        var nuevaHuella = verificacion == VerificacionClave.CorrectaRequiereActualizar ? protector.Calcular(clave) : null;
        turno.Dispose();

        var sesion = await CrearSesionAsync(cuenta, nuevaHuella, ct);
        return sesion is null ? ResultadoInicioSesion.Rechazada : new ResultadoInicioSesion(EstadoInicioSesion.Iniciada, sesion, null);
    }

    public async Task<IdentidadSesion?> ValidarAsync(string? token, CancellationToken ct)
    {
        var huella = tokens.Huella(token);
        if (huella is null) return null;
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var sesion = await unidad.Sesiones.ObtenerPorHuellaAsync(huella, ct);
        var ahora = ReglasCuentas.AhoraUtc(reloj);
        if (sesion is null || sesion.Revocada || ahora >= sesion.CaducaUtc || !PuedeEntrar(sesion.Cuenta, sesion.DocenteActivo))
            return null;
        var cuenta = sesion.Cuenta;
        return new IdentidadSesion(sesion.Id, cuenta.Id, cuenta.Usuario, cuenta.Nombre, PerfilesCuenta.Rol(cuenta.Perfil),
            cuenta.DocenteId, sesion.CaducaUtc);
    }

    public async Task CerrarAsync(string? token, CancellationToken ct)
    {
        var huella = tokens.Huella(token);
        if (huella is null) return;
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var sesion = await unidad.Sesiones.ObtenerPorHuellaAsync(huella, ct);
        if (sesion is null || sesion.Revocada) return;
        var ahora = ReglasCuentas.AhoraUtc(reloj);
        await unidad.Sesiones.RevocarAsync(sesion.Id, ahora, MotivoRevocacion.CierreSesion, ct);
        await ReglasCuentas.AuditarAsync(unidad, sesion.Cuenta.Id, AccionAuditoriaCuenta.CierreSesion, sesion.Cuenta.Id, ahora, ct);
        await unidad.ConfirmarAsync(ct);
    }

    private static bool PuedeEntrar(CuentaUsuario cuenta, bool docenteActivo) =>
        cuenta.Activo && (cuenta.Perfil == PerfilCuenta.Administracion || (cuenta.DocenteId is not null && docenteActivo));

    private async Task<(CuentaUsuario? Cuenta, bool DocenteActivo)> LeerCuentaAsync(string usuario, CancellationToken ct)
    {
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var cuenta = await unidad.Cuentas.ObtenerPorUsuarioAsync(usuario, ct);
        return (cuenta, await DocenteActivoAsync(unidad, cuenta, ct));
    }

    private static async Task<bool> DocenteActivoAsync(IUnidadSeguridad unidad, CuentaUsuario? cuenta, CancellationToken ct) =>
        cuenta?.DocenteId is { } docenteId && await unidad.Docentes.ObtenerAsync(docenteId, ct) is { Activo: true };

    private async Task<SesionIniciada?> CrearSesionAsync(CuentaUsuario leida, string? nuevaHuella, CancellationToken ct)
    {
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var cuenta = await unidad.Cuentas.ObtenerAsync(leida.Id, ct);
        // Si la cuenta cambió mientras se verificaba la clave (otra clave, desactivación), la verificación ya no vale.
        if (cuenta is null || cuenta.Version != leida.Version || !PuedeEntrar(cuenta, await DocenteActivoAsync(unidad, cuenta, ct)))
            return null;
        var ahora = ReglasCuentas.AhoraUtc(reloj);
        if (nuevaHuella is not null)
        {
            cuenta.CambiarHuellaClave(nuevaHuella);
            await unidad.Cuentas.ActualizarAsync(cuenta, ct);
        }
        await unidad.Sesiones.EliminarVencidasAsync(cuenta.Id, ahora - TimeSpan.FromDays(opciones.DiasConservarVencidas), ct);
        var token = tokens.Generar();
        var sesion = new SesionNueva(Guid.CreateVersion7(), cuenta.Id, token.Huella, ahora, ahora + opciones.Duracion);
        await unidad.Sesiones.AgregarAsync(sesion, ct);
        await unidad.Sesiones.RevocarExcedentesAsync(cuenta.Id, opciones.MaximoSesionesPorCuenta, ahora, ct);
        await ReglasCuentas.AuditarAsync(unidad, cuenta.Id, AccionAuditoriaCuenta.InicioSesion, cuenta.Id, ahora, ct);
        await unidad.ConfirmarAsync(ct);
        return new SesionIniciada(token.Valor, sesion.Id, cuenta.Nombre, cuenta.Perfil.ToString(), sesion.CaducaUtc) { Usuario = cuenta.Usuario };
    }
}
