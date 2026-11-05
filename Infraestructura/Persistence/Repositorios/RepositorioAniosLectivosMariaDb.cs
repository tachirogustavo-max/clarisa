using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Consultas;
using Clarisa.Domain.Common;
using Clarisa.Domain.Entities;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

internal sealed class RepositorioAniosLectivosMariaDb(SesionMariaDb sesion) : IRepositorioAniosLectivos
{
    private const string Columnas = "SELECT id, nombre, inicio, fin, lapso_actual_id, version FROM anios_lectivos";

    public Task<AnioLectivo?> ObtenerAsync(Guid id, CancellationToken ct) =>
        sesion.PrimeroAsync(Columnas + " WHERE id = @id", Convertir, ct, Parametros.Id("@id", id));

    public Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken ct) =>
        sesion.ExisteAsync("SELECT 1 FROM anios_lectivos WHERE nombre_clave = @clave AND (@excluir IS NULL OR id <> @excluir)", ct,
            Parametros.Texto("@clave", Textos.Clave(nombre)), Parametros.IdOpcional("@excluir", excluirId));

    public async Task<Pagina<AnioLectivo>> ListarAsync(ConsultaPagina consulta, CancellationToken ct)
    {
        var total = await sesion.EnteroAsync("SELECT COUNT(*) FROM anios_lectivos", ct);
        var filas = await sesion.PaginaAsync(Columnas + " ORDER BY inicio DESC, id LIMIT @tamanio OFFSET @desde", Convertir,
            consulta.Tamanio, ct, Parametros.Entero("@tamanio", consulta.Tamanio), Parametros.Entero("@desde", consulta.Desplazamiento));
        return new Pagina<AnioLectivo>(filas, consulta.Numero, consulta.Tamanio, (int)total);
    }

    public Task AgregarAsync(AnioLectivo anio, CancellationToken ct) =>
        sesion.EjecutarAsync("""
            INSERT INTO anios_lectivos (id, nombre, nombre_clave, inicio, fin, lapso_actual_id, version)
            VALUES (@id, @nombre, @clave, @inicio, @fin, @lapso, @version)
            """, ct, [.. Datos(anio), Parametros.Largo("@version", anio.Version)]);

    public async Task<long> ActualizarAsync(AnioLectivo anio, CancellationToken ct)
    {
        var filas = await sesion.EjecutarAsync("""
            UPDATE anios_lectivos SET nombre = @nombre, nombre_clave = @clave, inicio = @inicio, fin = @fin,
                lapso_actual_id = @lapso, version = version + 1
            WHERE id = @id AND version = @version
            """, ct, [.. Datos(anio), Parametros.Largo("@version", anio.Version)]);
        await sesion.ExigirUnaFilaAsync(filas, "anios_lectivos", anio.Id, ct);
        return anio.Version + 1;
    }

    private static Parametro[] Datos(AnioLectivo anio) =>
    [
        Parametros.Id("@id", anio.Id),
        Parametros.Texto("@nombre", anio.Nombre),
        Parametros.Texto("@clave", Textos.Clave(anio.Nombre)),
        Parametros.Fecha("@inicio", anio.Fechas.Inicio),
        Parametros.Fecha("@fin", anio.Fechas.Fin),
        Parametros.IdOpcional("@lapso", anio.LapsoActualId)
    ];

    private static AnioLectivo Convertir(MySqlDataReader fila) =>
        new(fila.Id(0), fila.GetString(1), fila.Fecha(2), fila.Fecha(3), fila.IdOpcional(4), fila.GetInt64(5));
}
