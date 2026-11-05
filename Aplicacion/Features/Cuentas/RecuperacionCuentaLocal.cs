using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Application.Common.Errores;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Cuentas;

public sealed record SolicitudRecuperacionCuenta(Guid OperacionId, string Usuario, string ClaveNueva, string Motivo)
{
    public override string ToString() => "Recuperación local de cuenta protegida";
}

public sealed class RecuperacionCuentaLocal(IFabricaRecuperacionCuenta fabrica, IProtectorClaves protector,
    IListaClavesProhibidas prohibidas, IReloj reloj)
{
    public async Task RecuperarAsync(OperadorRecuperacion operador, SolicitudRecuperacionCuenta solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operador);
        var datos = Error.SolicitudRequerida(solicitud);
        if (datos.OperacionId == Guid.Empty) throw Error.DatosInvalidos("Falta el identificador de operación.");
        ExigirTexto(operador.IdentidadWindows, 184, "Identidad del operador");
        ExigirTexto(operador.UsuarioBase, 80, "Usuario de base");
        ExigirTexto(datos.Motivo, 200, "Motivo");
        var usuario = ReglasCuentas.UsuarioNormalizado(datos.Usuario);
        CuentaUsuario anterior;
        await using (var lectura = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct))
            anterior = await ExigirCuentaAsync(lectura, usuario, ct);
        var nueva = PoliticaClaves.ValidarNueva(datos.ClaveNueva, usuario, prohibidas);
        if (protector.Verificar(nueva, anterior.HuellaClave) != VerificacionClave.Incorrecta)
            throw Error.DatosInvalidos("Elige una contraseña diferente de la anterior.", "ClaveNueva");
        var huella = protector.Calcular(nueva);
        ct.ThrowIfCancellationRequested();
        await using var escritura = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var cuenta = await ExigirCuentaAsync(escritura, usuario, ct);
        if (cuenta.Id != anterior.Id || cuenta.Version != anterior.Version || cuenta.HuellaClave != anterior.HuellaClave)
            throw Error.VersionObsoleta();
        cuenta.CambiarHuellaClave(huella);
        var version = await escritura.Cuentas.ActualizarAsync(cuenta, ct);
        var ahora = ReglasCuentas.AhoraUtc(reloj);
        await escritura.Sesiones.RevocarDeCuentaAsync(cuenta.Id, ahora, MotivoRevocacion.CambioClave, ct);
        await escritura.RegistrarRecuperacionAsync(new(datos.OperacionId, cuenta.Id, version, operador, datos.Motivo.Trim(), ahora), ct);
        await escritura.ConfirmarAsync(ct);
    }

    private static async Task<CuentaUsuario> ExigirCuentaAsync(IUnidadSeguridad unidad, string usuario, CancellationToken ct)
    {
        var cuenta = await unidad.Cuentas.ObtenerPorUsuarioAsync(usuario, ct);
        return cuenta is { Activo: true, Perfil: PerfilCuenta.Administracion } ? cuenta
            : throw Error.EstadoNoPermitido("Solo se recuperan cuentas administrativas activas. Revisa el usuario; no se creará ni reactivará una cuenta.");
    }

    private static void ExigirTexto(string? valor, int maximo, string nombre)
    {
        if (string.IsNullOrWhiteSpace(valor) || valor.Length > maximo || valor.Any(char.IsControl))
            throw Error.DatosInvalidos($"{nombre}: escribe un valor válido de hasta {maximo} caracteres.");
    }
}
