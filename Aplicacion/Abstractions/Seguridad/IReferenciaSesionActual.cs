using Clarisa.Application.Common.Seguridad;

namespace Clarisa.Application.Abstractions.Seguridad;

public sealed record ReferenciaSesionActual(Guid SesionId, Guid UsuarioId, Rol Rol, Guid? DocenteId);
public interface IReferenciaSesionActual
{
    ReferenciaSesionActual? Obtener();
}
