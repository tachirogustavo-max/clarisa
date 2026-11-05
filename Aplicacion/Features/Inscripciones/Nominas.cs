using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Errores;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Inscripciones;

internal sealed record Inscrito(Inscripcion Inscripcion, Estudiante Estudiante);

internal static class Nominas
{
    public static async Task<IReadOnlyList<Inscrito>> ActivosAsync(IUnidadTrabajo unidad, Guid seccionId, CancellationToken ct)
    {
        var inscripciones = await unidad.Inscripciones.ListarActivasAsync(seccionId, ct);
        var estudiantes = await EstudiantesDeAsync(unidad, inscripciones, ct);
        return inscripciones
            .Select(inscripcion => estudiantes.TryGetValue(inscripcion.EstudianteId, out var estudiante)
                ? new Inscrito(inscripcion, estudiante)
                : throw Error.ReferenciaInvalida("Una inscripción apunta a un estudiante inexistente."))
            .OrderBy(inscrito => inscrito.Estudiante.Apellidos, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(inscrito => inscrito.Estudiante.Nombres, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(inscrito => inscrito.Estudiante.Id)
            .ToList();
    }

    public static async Task<Dictionary<Guid, Estudiante>> EstudiantesDeAsync(IUnidadTrabajo unidad,
        IEnumerable<Inscripcion> inscripciones, CancellationToken ct)
    {
        var ids = inscripciones.Select(inscripcion => inscripcion.EstudianteId).Distinct().ToList();
        return (await unidad.Estudiantes.ObtenerVariosAsync(ids, ct)).ToDictionary(estudiante => estudiante.Id);
    }
}
