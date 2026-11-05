using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Domain.Entities;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

internal sealed class RepositorioInscripcionesMariaDb(SesionMariaDb sesion) : IRepositorioInscripciones
{
    private const string Columnas =
        "SELECT id, estudiante_id, seccion_id, anio_lectivo_id, fecha, estado, fecha_retiro, version FROM inscripciones";

    public Task<Inscripcion?> ObtenerAsync(Guid id, CancellationToken ct) =>
        sesion.PrimeroAsync(Columnas + " WHERE id = @id", Convertir, ct, Parametros.Id("@id", id));

    public Task<IReadOnlyList<Inscripcion>> ObtenerVariasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        ConsultaPorIds.ListarAsync(sesion, Columnas + " WHERE id IN ({0})", ids, Convertir, ct);

    public Task<bool> ExisteActivaEnSeccionAsync(Guid estudianteId, Guid seccionId, CancellationToken ct) =>
        sesion.ExisteAsync("SELECT 1 FROM inscripciones WHERE estudiante_id = @estudiante AND seccion_id = @seccion AND estado = 1", ct,
            Parametros.Id("@estudiante", estudianteId), Parametros.Id("@seccion", seccionId));

    public Task<IReadOnlyList<Inscripcion>> ListarActivasAsync(Guid seccionId, CancellationToken ct) =>
        sesion.ListarAsync(Columnas + " WHERE seccion_id = @seccion AND estado = 1 ORDER BY id", Convertir, ct,
            Parametros.Id("@seccion", seccionId));

    public Task<bool> ExistenEnSeccionAsync(Guid seccionId, CancellationToken ct) =>
        sesion.ExisteAsync("SELECT 1 FROM inscripciones WHERE seccion_id = @seccion", ct, Parametros.Id("@seccion", seccionId));

    public Task AgregarAsync(Inscripcion inscripcion, CancellationToken ct) =>
        sesion.EjecutarAsync("""
            INSERT INTO inscripciones (id, estudiante_id, seccion_id, anio_lectivo_id, fecha, estado, fecha_retiro, version)
            VALUES (@id, @estudiante, @seccion, @anio, @fecha, @estado, @retiro, @version)
            """, ct, Datos(inscripcion));

    // Estudiante, sección, año y fecha de inscripción no cambian: se incluyen en la condición para no reescribirlos.
    public async Task<long> ActualizarAsync(Inscripcion inscripcion, CancellationToken ct)
    {
        var filas = await sesion.EjecutarAsync("""
            UPDATE inscripciones SET estado = @estado, fecha_retiro = @retiro, version = version + 1
            WHERE id = @id AND estudiante_id = @estudiante AND seccion_id = @seccion AND anio_lectivo_id = @anio AND fecha = @fecha
              AND version = @version
            """, ct, Datos(inscripcion));
        await sesion.ExigirUnaFilaAsync(filas, "inscripciones", inscripcion.Id, ct);
        return inscripcion.Version + 1;
    }

    private static Parametro[] Datos(Inscripcion inscripcion) =>
    [
        Parametros.Id("@id", inscripcion.Id),
        Parametros.Id("@estudiante", inscripcion.EstudianteId),
        Parametros.Id("@seccion", inscripcion.SeccionId),
        Parametros.Id("@anio", inscripcion.AnioLectivoId),
        Parametros.Fecha("@fecha", inscripcion.Fecha),
        Parametros.Entero("@estado", (int)inscripcion.Estado),
        Parametros.FechaOpcional("@retiro", inscripcion.FechaRetiro),
        Parametros.Largo("@version", inscripcion.Version)
    ];

    private static Inscripcion Convertir(MySqlDataReader fila) =>
        new(fila.Id(0), fila.Id(1), fila.Id(2), fila.Id(3), fila.Fecha(4), fila.Enumerado<EstadoInscripcion>(5), fila.FechaOpcional(6),
            fila.GetInt64(7));
}
