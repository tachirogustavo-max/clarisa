using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Cuentas;

internal static class PerfilesCuenta
{
    // Solo los nombres exactos: un número o un perfil desconocido enviado por el cliente no se convierte en permiso.
    public static PerfilCuenta Desde(string? perfil) => perfil switch
    {
        nameof(PerfilCuenta.Administracion) => PerfilCuenta.Administracion,
        nameof(PerfilCuenta.Docente) => PerfilCuenta.Docente,
        _ => throw Error.DatosInvalidos("El perfil debe ser Administracion o Docente.", "Perfil")
    };

    public static Rol Rol(PerfilCuenta perfil) => perfil switch
    {
        PerfilCuenta.Administracion => Common.Seguridad.Rol.Administracion,
        PerfilCuenta.Docente => Common.Seguridad.Rol.Docente,
        _ => throw new ArgumentOutOfRangeException(nameof(perfil))
    };
}
