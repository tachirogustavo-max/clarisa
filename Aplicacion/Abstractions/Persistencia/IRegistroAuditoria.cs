using Clarisa.Application.Common.Auditoria;

namespace Clarisa.Application.Abstractions.Persistencia;

public interface IRegistroAuditoria
{
    Task RegistrarAsync(EventoAuditoria evento, CancellationToken ct);
}
