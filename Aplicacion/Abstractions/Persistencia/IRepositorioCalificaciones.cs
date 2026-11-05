using Clarisa.Application.Common.Auditoria;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Abstractions.Persistencia;

public interface IRepositorioCalificaciones
{
    Task<IReadOnlyList<Calificacion>> ListarAsync(Guid lapsoId, Guid materiaId, IReadOnlyCollection<Guid> inscripcionIds,
        CancellationToken ct);
    Task<IReadOnlyList<Calificacion>> ListarPorInscripcionesAsync(IReadOnlyCollection<Guid> inscripcionIds, CancellationToken ct);
    Task<bool> ExistenEnSeccionMateriaAsync(Guid seccionId, Guid materiaId, CancellationToken ct);
    Task<bool> ExistenEnSeccionAsync(Guid seccionId, CancellationToken ct);
    // Resultados guardados de una materia en todas las secciones del grado y año, contados por tipo; incluye inscripciones retiradas.
    Task<IReadOnlyDictionary<TipoResultado, int>> ContarPorTipoEnGradoAsync(Guid anioLectivoId, string grado, Guid materiaId,
        CancellationToken ct);
    // Activar una política con menos decimales de captura no puede dejar notas guardadas fuera de su precisión.
    Task<bool> ExistenConMasDecimalesAsync(Guid anioLectivoId, int decimales, CancellationToken ct);
    Task AgregarAsync(Calificacion calificacion, CancellationToken ct);
    Task<long> ActualizarAsync(Calificacion calificacion, CancellationToken ct);
    Task RegistrarCambioAsync(CambioCalificacion cambio, CancellationToken ct);
}
