using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Resultados;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Cuentas;

// Procedimiento local y explícito del servidor: solo crea la primera cuenta cuando todavía no existe ninguna.
public sealed class PreparacionCuentas(IFabricaUnidadSeguridad fabrica, IProtectorClaves protector, IListaClavesProhibidas prohibidas,
    IReloj reloj)
{
    public async Task<bool> HayCuentasAsync(CancellationToken ct)
    {
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        return await unidad.Cuentas.ExisteAlgunaAsync(ct);
    }

    public async Task<RegistroVersionado> CrearPrimerAdministradorAsync(PrimerAdministrador solicitud, CancellationToken ct)
    {
        var datos = Error.SolicitudRequerida(solicitud);
        var usuario = ReglasCuentas.UsuarioNormalizado(datos.Usuario);
        var clave = PoliticaClaves.ValidarNueva(datos.Clave, usuario, prohibidas);
        var cuenta = Error.DesdeDominio(() => new CuentaUsuario(datos.Id, usuario, datos.Nombre, PerfilCuenta.Administracion, null,
            protector.Calcular(clave), activo: true, version: 1));

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        if (await unidad.Cuentas.ExisteAlgunaAsync(ct))
            throw Error.EstadoNoPermitido("Ya existen cuentas. La preparación inicial no se repite; usa la administración de usuarios.");
        await unidad.Cuentas.AgregarAsync(cuenta, ct);
        await ReglasCuentas.AuditarAsync(unidad, cuenta.Id, AccionAuditoriaCuenta.PreparacionInicial, cuenta.Id,
            ReglasCuentas.AhoraUtc(reloj), ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(cuenta.Id, cuenta.Version);
    }
}
