using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Domain.Common;
using Clarisa.Domain.Entities;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

internal sealed class RepositorioPlanEstudiosMariaDb(SesionMariaDb sesion) : IRepositorioPlanEstudios
{
    private const string Columnas = "SELECT id, anio_lectivo_id, grado, materia_id, modalidad, version FROM plan_materias";
    public Task<long> ContarAsync(Guid anioLectivoId, CancellationToken ct) =>
        sesion.EnteroAsync("SELECT COUNT(*) FROM plan_materias WHERE anio_lectivo_id = @anio", ct, Parametros.Id("@anio", anioLectivoId));

    public Task<PlanMateria?> ObtenerAsync(Guid id, CancellationToken ct) =>
        sesion.PrimeroAsync(Columnas + " WHERE id = @id", Convertir, ct, Parametros.Id("@id", id));

    public Task<PlanMateria?> BuscarAsync(Guid anioLectivoId, string grado, Guid materiaId, CancellationToken ct) =>
        sesion.PrimeroAsync(Columnas + " WHERE anio_lectivo_id = @anio AND grado_clave = @grado AND materia_id = @materia", Convertir, ct,
            Parametros.Id("@anio", anioLectivoId), Parametros.Texto("@grado", Textos.Clave(grado)), Parametros.Id("@materia", materiaId));

    public Task<IReadOnlyList<PlanMateria>> ListarAsync(Guid anioLectivoId, string? grado, int maximo, CancellationToken ct) =>
        sesion.ListarAsync(Columnas + " WHERE anio_lectivo_id = @anio AND (@grado IS NULL OR grado_clave = @grado) ORDER BY grado_clave, materia_id",
            Convertir, maximo, ct, Parametros.Id("@anio", anioLectivoId),
            Parametros.Texto("@grado", grado is null ? null : Textos.Clave(grado)));

    public Task AgregarAsync(PlanMateria plan, CancellationToken ct) =>
        sesion.EjecutarAsync("""
            INSERT INTO plan_materias (id, anio_lectivo_id, grado, grado_clave, materia_id, modalidad, version)
            VALUES (@id, @anio, @grado, @grado_clave, @materia, @modalidad, @version)
            """, ct, Datos(plan));

    public async Task<long> ActualizarAsync(PlanMateria plan, CancellationToken ct)
    {
        var filas = await sesion.EjecutarAsync(
            "UPDATE plan_materias SET modalidad = @modalidad, version = version + 1 WHERE id = @id AND version = @version", ct, Datos(plan));
        await sesion.ExigirUnaFilaAsync(filas, "plan_materias", plan.Id, ct);
        return plan.Version + 1;
    }

    public async Task EliminarAsync(Guid id, long versionEsperada, CancellationToken ct)
    {
        var filas = await sesion.EjecutarAsync("DELETE FROM plan_materias WHERE id = @id AND version = @version", ct,
            Parametros.Id("@id", id), Parametros.Largo("@version", versionEsperada));
        await sesion.ExigirUnaFilaAsync(filas, "plan_materias", id, ct);
    }

    private static Parametro[] Datos(PlanMateria plan) =>
    [
        Parametros.Id("@id", plan.Id),
        Parametros.Id("@anio", plan.AnioLectivoId),
        Parametros.Texto("@grado", plan.Grado),
        Parametros.Texto("@grado_clave", plan.GradoClave),
        Parametros.Id("@materia", plan.MateriaId),
        Parametros.Entero("@modalidad", (int)plan.Modalidad),
        Parametros.Largo("@version", plan.Version)
    ];

    private static PlanMateria Convertir(MySqlDataReader fila) =>
        new(fila.Id(0), fila.Id(1), fila.GetString(2), fila.Id(3), fila.Enumerado<ModalidadEvaluacion>(4), fila.GetInt64(5));
}
