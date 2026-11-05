using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Consultas;
using Clarisa.Domain.Common;
using Clarisa.Domain.Entities;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

internal sealed class RepositorioSeccionesMariaDb(SesionMariaDb sesion) : IRepositorioSecciones
{
    private const string Columnas = "SELECT id, anio_lectivo_id, grado, nombre, version FROM secciones";
    public Task<bool> ExisteGradoAsync(Guid anioLectivoId, string grado, CancellationToken ct) =>
        sesion.ExisteAsync("SELECT 1 FROM secciones WHERE anio_lectivo_id = @anio AND grado_clave = @grado", ct,
            Parametros.Id("@anio", anioLectivoId), Parametros.Texto("@grado", Textos.Clave(grado)));
    public Task<IReadOnlyList<string>> GradosAsync(Guid anioLectivoId, CancellationToken ct) =>
        sesion.ListarAsync("SELECT MIN(grado) FROM secciones WHERE anio_lectivo_id = @anio GROUP BY grado_clave ORDER BY grado_clave",
            fila => fila.GetString(0), 100, ct, Parametros.Id("@anio", anioLectivoId));

    public Task<Seccion?> ObtenerAsync(Guid id, CancellationToken ct) =>
        sesion.PrimeroAsync(Columnas + " WHERE id = @id", Convertir, ct, Parametros.Id("@id", id));

    public Task<bool> ExisteNombreAsync(Guid anioLectivoId, string grado, string nombre, Guid? excluirId, CancellationToken ct) =>
        sesion.ExisteAsync("""
            SELECT 1 FROM secciones
            WHERE anio_lectivo_id = @anio AND grado_clave = @grado AND nombre_clave = @nombre AND (@excluir IS NULL OR id <> @excluir)
            """, ct, Parametros.Id("@anio", anioLectivoId), Parametros.Texto("@grado", Textos.Clave(grado)),
            Parametros.Texto("@nombre", Textos.Clave(nombre)), Parametros.IdOpcional("@excluir", excluirId));

    public async Task<Pagina<Seccion>> ListarPorAnioAsync(Guid anioLectivoId, ConsultaPagina consulta, CancellationToken ct)
    {
        var anio = Parametros.Id("@anio", anioLectivoId);
        var total = await sesion.EnteroAsync("SELECT COUNT(*) FROM secciones WHERE anio_lectivo_id = @anio", ct, anio);
        var filas = await sesion.PaginaAsync(
            Columnas + " WHERE anio_lectivo_id = @anio ORDER BY grado, nombre, id LIMIT @tamanio OFFSET @desde", Convertir,
            consulta.Tamanio, ct, anio, Parametros.Entero("@tamanio", consulta.Tamanio), Parametros.Entero("@desde", consulta.Desplazamiento));
        return new Pagina<Seccion>(filas, consulta.Numero, consulta.Tamanio, (int)total);
    }

    public Task AgregarAsync(Seccion seccion, CancellationToken ct) =>
        sesion.EjecutarAsync("""
            INSERT INTO secciones (id, anio_lectivo_id, grado, grado_clave, nombre, nombre_clave, version)
            VALUES (@id, @anio, @grado, @grado_clave, @nombre, @nombre_clave, @version)
            """, ct, Datos(seccion));

    public async Task<long> ActualizarAsync(Seccion seccion, CancellationToken ct)
    {
        var filas = await sesion.EjecutarAsync("""
            UPDATE secciones SET grado = @grado, grado_clave = @grado_clave, nombre = @nombre, nombre_clave = @nombre_clave,
                version = version + 1
            WHERE id = @id AND anio_lectivo_id = @anio AND version = @version
            """, ct, Datos(seccion));
        await sesion.ExigirUnaFilaAsync(filas, "secciones", seccion.Id, ct);
        return seccion.Version + 1;
    }

    public async Task EliminarAsync(Guid id, long versionEsperada, CancellationToken ct)
    {
        var filas = await sesion.EjecutarAsync("DELETE FROM secciones WHERE id = @id AND version = @version", ct,
            Parametros.Id("@id", id), Parametros.Largo("@version", versionEsperada));
        await sesion.ExigirUnaFilaAsync(filas, "secciones", id, ct);
    }

    private static Parametro[] Datos(Seccion seccion) =>
    [
        Parametros.Id("@id", seccion.Id),
        Parametros.Id("@anio", seccion.AnioLectivoId),
        Parametros.Texto("@grado", seccion.Grado),
        Parametros.Texto("@grado_clave", Textos.Clave(seccion.Grado)),
        Parametros.Texto("@nombre", seccion.Nombre),
        Parametros.Texto("@nombre_clave", Textos.Clave(seccion.Nombre)),
        Parametros.Largo("@version", seccion.Version)
    ];

    private static Seccion Convertir(MySqlDataReader fila) =>
        new(fila.Id(0), fila.Id(1), fila.GetString(2), fila.GetString(3), fila.GetInt64(4));
}
