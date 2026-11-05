using System.Security.Claims;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Seguridad;

namespace Clarisa.Api.Http;

public static class ActorHttp
{
    public static Actor ActorActual(this HttpContext contexto)
    {
        var usuario = contexto.User;
        if (usuario.Identity?.IsAuthenticated != true || !Guid.TryParse(usuario.FindFirstValue(ClaimTypes.NameIdentifier), out var id) || id == Guid.Empty)
            throw Error.AccesoDenegado();
        var roles = usuario.FindAll(ClaimTypes.Role).Select(c => c.Value).Select(valor => valor switch {
            "Administracion" => (Rol?)Rol.Administracion, "Docente" => Rol.Docente, _ => null
        }).Where(r => r.HasValue).Select(r => r!.Value).ToArray();
        Guid? docente = Guid.TryParse(usuario.FindFirstValue("docente_id"), out var vinculado) && vinculado != Guid.Empty ? vinculado : null;
        return new Actor(id, roles, docente);
    }

    public static void ExigirId(Guid ruta, Guid cuerpo)
    {
        if (ruta == Guid.Empty || ruta != cuerpo) throw Error.DatosInvalidos("El registro de la ruta no coincide con el enviado.", "Id");
    }
}
