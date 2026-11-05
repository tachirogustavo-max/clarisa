using System.Globalization;
using System.Security.Claims;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Application.Features.Cuentas;

namespace Clarisa.Api.Security;

// Todas las afirmaciones salen de la sesión y la cuenta leídas en el servidor durante la solicitud.
public static class PrincipalSesion
{
    public const string DocenteId = "docente_id";
    public const string SesionId = "sesion_id";
    public const string Nombre = "nombre";
    public const string Caduca = "caduca";

    public static ClaimsPrincipal Crear(IdentidadSesion identidad, string esquema)
    {
        List<Claim> afirmaciones =
        [
            new(ClaimTypes.NameIdentifier, identidad.UsuarioId.ToString()),
            new(ClaimTypes.Name, identidad.Usuario),
            new(ClaimTypes.Role, identidad.Rol.ToString()),
            new(SesionId, identidad.SesionId.ToString()),
            new(Nombre, identidad.Nombre),
            new(Caduca, identidad.Caduca.ToString("O", CultureInfo.InvariantCulture))
        ];
        if (identidad.Rol == Rol.Docente && identidad.DocenteId is { } docente) afirmaciones.Add(new(DocenteId, docente.ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(afirmaciones, esquema, ClaimTypes.Name, ClaimTypes.Role));
    }

    public static SesionVisible? Visible(ClaimsPrincipal usuario) =>
        usuario.Identity?.IsAuthenticated == true
        && usuario.FindFirstValue(Nombre) is { } nombre && usuario.FindFirstValue(ClaimTypes.Role) is { } perfil
        && DateTimeOffset.TryParse(usuario.FindFirstValue(Caduca), CultureInfo.InvariantCulture, DateTimeStyles.None, out var caduca)
            ? new SesionVisible(nombre, perfil, caduca, usuario.FindFirstValue(ClaimTypes.Name))
            : null;
}

public sealed record SesionVisible(string Nombre, string Perfil, DateTimeOffset Caduca, string? Usuario = null);
