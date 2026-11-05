namespace Clarisa.Application.Abstractions.Seguridad;

// Disponible en la unidad académica para que la baja del docente revoque en la misma transacción las sesiones de su cuenta,
// sin dar a los casos académicos acceso a cuentas, huellas ni tokens.
public interface IRevocacionAccesosDocente
{
    Task<int> RevocarSesionesAsync(Guid docenteId, DateTimeOffset ahoraUtc, CancellationToken ct);
}
