using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Domain.Entities;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

internal sealed class RepositorioAsignacionesDocentesMariaDb(SesionMariaDb sesion) : IRepositorioAsignacionesDocentes
{
    private const string Columnas = "SELECT id, seccion_id, materia_id, docente_id, version FROM asignaciones_docentes";

    public Task<AsignacionDocente?> ObtenerAsync(Guid id, CancellationToken ct) =>
        sesion.PrimeroAsync(Columnas + " WHERE id = @id", Convertir, ct, Parametros.Id("@id", id));

    public Task<bool> ExisteAsync(Guid seccionId, Guid materiaId, Guid docenteId, CancellationToken ct) =>
        sesion.ExisteAsync("SELECT 1 FROM asignaciones_docentes WHERE seccion_id = @seccion AND materia_id = @materia AND docente_id = @docente",
            ct, Parametros.Id("@seccion", seccionId), Parametros.Id("@materia", materiaId), Parametros.Id("@docente", docenteId));

    public Task<bool> DocenteTieneSeccionAsync(Guid docenteId, Guid seccionId, CancellationToken ct) =>
        sesion.ExisteAsync("SELECT 1 FROM asignaciones_docentes WHERE docente_id = @docente AND seccion_id = @seccion", ct,
            Parametros.Id("@docente", docenteId), Parametros.Id("@seccion", seccionId));

    public Task<bool> ExistenEnSeccionAsync(Guid seccionId, CancellationToken ct) =>
        sesion.ExisteAsync("SELECT 1 FROM asignaciones_docentes WHERE seccion_id = @seccion", ct, Parametros.Id("@seccion", seccionId));

    public Task<IReadOnlyList<AsignacionDocente>> ListarPorSeccionAsync(Guid seccionId, CancellationToken ct) =>
        sesion.ListarAsync(Columnas + " WHERE seccion_id = @seccion ORDER BY materia_id, docente_id, id", Convertir, ct,
            Parametros.Id("@seccion", seccionId));

    public Task<IReadOnlyList<AsignacionDocente>> ListarPorDocenteAsync(Guid docenteId, CancellationToken ct) =>
        sesion.ListarAsync(Columnas + " WHERE docente_id = @docente ORDER BY seccion_id, materia_id, id", Convertir, ct,
            Parametros.Id("@docente", docenteId));

    public Task AgregarAsync(AsignacionDocente asignacion, CancellationToken ct) =>
        sesion.EjecutarAsync("""
            INSERT INTO asignaciones_docentes (id, seccion_id, materia_id, docente_id, version)
            VALUES (@id, @seccion, @materia, @docente, @version)
            """, ct, Datos(asignacion));

    // Solo el docente cambia; sección y materia forman parte de la condición.
    public async Task<long> ActualizarAsync(AsignacionDocente asignacion, CancellationToken ct)
    {
        var filas = await sesion.EjecutarAsync("""
            UPDATE asignaciones_docentes SET docente_id = @docente, version = version + 1
            WHERE id = @id AND seccion_id = @seccion AND materia_id = @materia AND version = @version
            """, ct, Datos(asignacion));
        await sesion.ExigirUnaFilaAsync(filas, "asignaciones_docentes", asignacion.Id, ct);
        return asignacion.Version + 1;
    }

    public async Task EliminarAsync(Guid id, long versionEsperada, CancellationToken ct)
    {
        var filas = await sesion.EjecutarAsync("DELETE FROM asignaciones_docentes WHERE id = @id AND version = @version", ct,
            Parametros.Id("@id", id), Parametros.Largo("@version", versionEsperada));
        await sesion.ExigirUnaFilaAsync(filas, "asignaciones_docentes", id, ct);
    }

    private static Parametro[] Datos(AsignacionDocente asignacion) =>
    [
        Parametros.Id("@id", asignacion.Id),
        Parametros.Id("@seccion", asignacion.SeccionId),
        Parametros.Id("@materia", asignacion.MateriaId),
        Parametros.Id("@docente", asignacion.DocenteId),
        Parametros.Largo("@version", asignacion.Version)
    ];

    private static AsignacionDocente Convertir(MySqlDataReader fila) =>
        new(fila.Id(0), fila.Id(1), fila.Id(2), fila.Id(3), fila.GetInt64(4));
}
