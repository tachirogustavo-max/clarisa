using System.Security;

namespace Clarisa.Desktop.Services;
public interface IClienteSesiones
{
    Task<ResultadoAcceso> IniciarAsync(Uri servidor, string usuario, SecureString clave, CancellationToken ct);
}

public sealed record SesionActual(string Nombre, string Perfil, DateTimeOffset Caduca, string? Usuario = null);
public sealed record ResultadoAcceso(SesionActual? Sesion, string? Error);
