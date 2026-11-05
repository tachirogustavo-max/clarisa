using System.Security.Claims;
using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Seguridad;

namespace Clarisa.Api.Security;

public sealed class ReferenciaSesionHttp(IHttpContextAccessor contexto) : IReferenciaSesionActual
{
    public ReferenciaSesionActual? Obtener()
    {
        var usuario = contexto.HttpContext?.User;
        if (usuario?.Identity?.IsAuthenticated != true) return null;
        if (!Guid.TryParse(usuario.FindFirstValue(PrincipalSesion.SesionId), out var sesion) || sesion == Guid.Empty
            || !Guid.TryParse(usuario.FindFirstValue(ClaimTypes.NameIdentifier), out var id) || id == Guid.Empty)
            throw Error.SesionNoValida();
        var rol = usuario.FindFirstValue(ClaimTypes.Role) switch
        {
            "Administracion" => Rol.Administracion,
            "Docente" => Rol.Docente,
            _ => throw Error.SesionNoValida()
        };
        Guid? docente = Guid.TryParse(usuario.FindFirstValue(PrincipalSesion.DocenteId), out var vinculo) && vinculo != Guid.Empty ? vinculo : null;
        return new(sesion, id, rol, docente);
    }
}
