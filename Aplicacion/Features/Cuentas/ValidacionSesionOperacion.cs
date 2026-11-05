using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Application.Common.Errores;

namespace Clarisa.Application.Features.Cuentas;

public static class ValidacionSesionOperacion
{
    public static void Exigir(ReferenciaSesionActual referencia, SesionAlmacenada? sesion, DateTimeOffset ahora)
    {
        if (sesion is null || sesion.Id != referencia.SesionId || sesion.Revocada || ahora >= sesion.CaducaUtc
            || sesion.Cuenta.Id != referencia.UsuarioId || !sesion.Cuenta.Activo
            || PerfilesCuenta.Rol(sesion.Cuenta.Perfil) != referencia.Rol || sesion.Cuenta.DocenteId != referencia.DocenteId
            || (sesion.Cuenta.DocenteId is not null && !sesion.DocenteActivo))
            throw Error.SesionNoValida();
    }
}
