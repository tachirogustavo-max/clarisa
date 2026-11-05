using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Infrastructure.Persistence.Sesion;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

// Usa la conexión y la transacción de la unidad académica: la baja del docente y la revocación se confirman o revierten juntas.
internal sealed class RevocacionAccesosDocenteMariaDb(SesionMariaDb sesion) : IRevocacionAccesosDocente
{
    public Task<int> RevocarSesionesAsync(Guid docenteId, DateTimeOffset ahoraUtc, CancellationToken ct) =>
        sesion.EjecutarAsync("""
            UPDATE sesiones_usuario s JOIN cuentas_usuario c ON c.id = s.cuenta_id
            SET s.revocada_utc_ticks = @ahora, s.motivo_revocacion = @motivo
            WHERE c.docente_id = @docente AND s.revocada_utc_ticks IS NULL
            """, ct, Parametros.Id("@docente", docenteId), Parametros.Instante("@ahora", ahoraUtc),
            Parametros.Entero("@motivo", (int)MotivoRevocacion.Desactivacion));
}
