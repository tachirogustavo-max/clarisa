using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Domain.Entities;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

internal sealed class RepositorioEscalaCualitativaMariaDb(SesionMariaDb sesion) : IRepositorioEscalaCualitativa
{
    private const int MaximoLetras = 100;
    private const string Columnas = "SELECT id, anio_lectivo_id, letra, descripcion, version FROM escala_cualitativa";

    public Task<LetraCualitativa?> ObtenerAsync(Guid id, CancellationToken ct) =>
        sesion.PrimeroAsync(Columnas + " WHERE id = @id", Convertir, ct, Parametros.Id("@id", id));

    public Task<IReadOnlyList<LetraCualitativa>> ListarAsync(Guid anioLectivoId, CancellationToken ct) =>
        sesion.ListarAsync(Columnas + " WHERE anio_lectivo_id = @anio ORDER BY letra", Convertir, MaximoLetras, ct,
            Parametros.Id("@anio", anioLectivoId));

    // El historial también cuenta: una letra usada alguna vez sigue explicando cambios anteriores.
    public Task<bool> EnUsoAsync(Guid anioLectivoId, string letra, CancellationToken ct) =>
        sesion.ExisteAsync("""
            SELECT 1 FROM calificaciones WHERE anio_lectivo_id = @anio AND letra = @letra
            UNION ALL
            SELECT 1 FROM cambios_calificacion h JOIN calificaciones c ON c.id = h.calificacion_id
            WHERE c.anio_lectivo_id = @anio AND (h.letra_anterior = @letra OR h.letra_nueva = @letra)
            """, ct, Parametros.Id("@anio", anioLectivoId), Parametros.Texto("@letra", letra));

    public Task AgregarAsync(LetraCualitativa letra, CancellationToken ct) =>
        sesion.EjecutarAsync("""
            INSERT INTO escala_cualitativa (id, anio_lectivo_id, letra, descripcion, version)
            VALUES (@id, @anio, @letra, @descripcion, @version)
            """, ct,
            Parametros.Id("@id", letra.Id),
            Parametros.Id("@anio", letra.AnioLectivoId),
            Parametros.Texto("@letra", letra.Letra),
            Parametros.Texto("@descripcion", letra.Descripcion),
            Parametros.Largo("@version", letra.Version));

    public async Task EliminarAsync(Guid id, long versionEsperada, CancellationToken ct)
    {
        var filas = await sesion.EjecutarAsync("DELETE FROM escala_cualitativa WHERE id = @id AND version = @version", ct,
            Parametros.Id("@id", id), Parametros.Largo("@version", versionEsperada));
        await sesion.ExigirUnaFilaAsync(filas, "escala_cualitativa", id, ct);
    }

    private static LetraCualitativa Convertir(MySqlDataReader fila) =>
        new(fila.Id(0), fila.Id(1), fila.GetString(2), fila.TextoOpcional(3), fila.GetInt64(4));
}
