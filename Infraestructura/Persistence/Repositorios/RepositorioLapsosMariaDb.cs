using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Domain.Common;
using Clarisa.Domain.Entities;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

internal sealed class RepositorioLapsosMariaDb(SesionMariaDb sesion) : IRepositorioLapsos
{
    private const string Columnas = "SELECT id, anio_lectivo_id, nombre, orden, inicio, fin, version FROM lapsos";

    public Task<Lapso?> ObtenerAsync(Guid id, CancellationToken ct) =>
        sesion.PrimeroAsync(Columnas + " WHERE id = @id", Convertir, ct, Parametros.Id("@id", id));

    public Task<IReadOnlyList<Lapso>> ListarPorAnioAsync(Guid anioLectivoId, CancellationToken ct) =>
        sesion.ListarAsync(Columnas + " WHERE anio_lectivo_id = @anio ORDER BY orden, id", Convertir, ct,
            Parametros.Id("@anio", anioLectivoId));

    public Task<bool> ExisteOrdenAsync(Guid anioLectivoId, int orden, Guid? excluirId, CancellationToken ct) =>
        sesion.ExisteAsync("SELECT 1 FROM lapsos WHERE anio_lectivo_id = @anio AND orden = @orden AND (@excluir IS NULL OR id <> @excluir)",
            ct, Parametros.Id("@anio", anioLectivoId), Parametros.Entero("@orden", orden), Parametros.IdOpcional("@excluir", excluirId));

    public Task<bool> ExisteNombreAsync(Guid anioLectivoId, string nombre, Guid? excluirId, CancellationToken ct) =>
        sesion.ExisteAsync("SELECT 1 FROM lapsos WHERE anio_lectivo_id = @anio AND nombre_clave = @clave AND (@excluir IS NULL OR id <> @excluir)",
            ct, Parametros.Id("@anio", anioLectivoId), Parametros.Texto("@clave", Textos.Clave(nombre)),
            Parametros.IdOpcional("@excluir", excluirId));

    public Task AgregarAsync(Lapso lapso, CancellationToken ct) =>
        sesion.EjecutarAsync("""
            INSERT INTO lapsos (id, anio_lectivo_id, nombre, nombre_clave, orden, inicio, fin, version)
            VALUES (@id, @anio, @nombre, @clave, @orden, @inicio, @fin, @version)
            """, ct, Datos(lapso));

    public async Task<long> ActualizarAsync(Lapso lapso, CancellationToken ct)
    {
        var filas = await sesion.EjecutarAsync("""
            UPDATE lapsos SET nombre = @nombre, nombre_clave = @clave, orden = @orden, inicio = @inicio, fin = @fin,
                version = version + 1
            WHERE id = @id AND anio_lectivo_id = @anio AND version = @version
            """, ct, Datos(lapso));
        await sesion.ExigirUnaFilaAsync(filas, "lapsos", lapso.Id, ct);
        return lapso.Version + 1;
    }

    private static Parametro[] Datos(Lapso lapso) =>
    [
        Parametros.Id("@id", lapso.Id),
        Parametros.Id("@anio", lapso.AnioLectivoId),
        Parametros.Texto("@nombre", lapso.Nombre),
        Parametros.Texto("@clave", Textos.Clave(lapso.Nombre)),
        Parametros.Entero("@orden", lapso.Orden),
        Parametros.Fecha("@inicio", lapso.Fechas.Inicio),
        Parametros.Fecha("@fin", lapso.Fechas.Fin),
        Parametros.Largo("@version", lapso.Version)
    ];

    private static Lapso Convertir(MySqlDataReader fila) =>
        new(fila.Id(0), fila.Id(1), fila.GetString(2), fila.GetInt32(3), fila.Fecha(4), fila.Fecha(5), fila.GetInt64(6));
}
