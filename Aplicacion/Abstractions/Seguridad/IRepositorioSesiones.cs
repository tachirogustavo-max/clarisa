using Clarisa.Domain.Entities;

namespace Clarisa.Application.Abstractions.Seguridad;

public enum MotivoRevocacion
{
    CierreSesion = 1,
    Desactivacion = 2,
    CambioPermisos = 3,
    CambioClave = 4,
    Exceso = 5
}

public sealed record SesionNueva(Guid Id, Guid CuentaId, string HuellaToken, DateTimeOffset CreadaUtc, DateTimeOffset CaducaUtc);

// La cuenta y el estado del docente vinculado se leen junto con la sesión, en la misma consulta.
public sealed record SesionAlmacenada(Guid Id, DateTimeOffset CaducaUtc, bool Revocada, CuentaUsuario Cuenta, bool DocenteActivo);

public interface IRepositorioSesiones
{
    Task AgregarAsync(SesionNueva sesion, CancellationToken ct);
    Task<SesionAlmacenada?> ObtenerPorHuellaAsync(string huellaToken, CancellationToken ct);
    Task<bool> RevocarAsync(Guid sesionId, DateTimeOffset ahoraUtc, MotivoRevocacion motivo, CancellationToken ct);
    Task<int> RevocarDeCuentaAsync(Guid cuentaId, DateTimeOffset ahoraUtc, MotivoRevocacion motivo, CancellationToken ct);
    Task<int> RevocarExcedentesAsync(Guid cuentaId, int conservar, DateTimeOffset ahoraUtc, CancellationToken ct);
    Task<int> EliminarVencidasAsync(Guid cuentaId, DateTimeOffset antesDeUtc, CancellationToken ct);
}
