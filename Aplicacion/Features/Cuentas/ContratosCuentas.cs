using Clarisa.Application.Common.Seguridad;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Cuentas;

// Origen: dirección de la estación según el transporte; solo se usa para limitar intentos, nunca para autorizar.
public sealed record IniciarSesion(string? Usuario, string? Clave, string? Origen)
{
    public override string ToString() => "Datos de acceso protegidos";
}

public enum EstadoInicioSesion
{
    Iniciada = 1,
    Rechazada = 2,
    Bloqueada = 3
}

// El token solo viaja hacia el transporte para escribir la cookie; no se registra ni se devuelve en el cuerpo.
public sealed record SesionIniciada(string Token, Guid SesionId, string Nombre, string Perfil, DateTimeOffset Caduca)
{
    public string Usuario { get; init; } = "";
    public override string ToString() => "Datos de acceso protegidos";
}

public sealed record ResultadoInicioSesion(EstadoInicioSesion Estado, SesionIniciada? Sesion, TimeSpan? ReintentarEn)
{
    public static ResultadoInicioSesion Rechazada { get; } = new(EstadoInicioSesion.Rechazada, null, null);
}

public sealed record IdentidadSesion(Guid SesionId, Guid UsuarioId, string Usuario, string Nombre, Rol Rol, Guid? DocenteId,
    DateTimeOffset Caduca)
{
    public string Perfil => Rol.ToString();
}

public sealed record CrearCuenta(Guid Id, string? Usuario, string? Nombre, string? Perfil, Guid? DocenteId, string? Clave)
{
    public override string ToString() => "Datos de acceso protegidos";
}

// Clave nula o vacía conserva la actual.
public sealed record ModificarCuenta(Guid Id, long VersionEsperada, string? Usuario, string? Nombre, string? Perfil, Guid? DocenteId,
    bool Activo, string? Clave)
{
    public override string ToString() => "Datos de acceso protegidos";
}

public sealed record PrimerAdministrador(Guid Id, string? Usuario, string? Nombre, string? Clave)
{
    public override string ToString() => "Datos de acceso protegidos";
}

public sealed record CuentaDatos(Guid Id, string Usuario, string Nombre, string Perfil, Guid? DocenteId, bool Activo, long Version)
{
    public static CuentaDatos Desde(CuentaUsuario cuenta) =>
        new(cuenta.Id, cuenta.Usuario, cuenta.Nombre, cuenta.Perfil.ToString(), cuenta.DocenteId, cuenta.Activo, cuenta.Version);
}
