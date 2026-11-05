using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Consultas;
using Clarisa.Domain.Common;
using Clarisa.Domain.Entities;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

internal sealed class RepositorioGradosMariaDb(SesionMariaDb sesion) : IRepositorioGrados
{
    private const string Columnas = "SELECT id, nombre, orden, activo, version FROM grados";

    private const string Filtro = """
         WHERE (@inactivos = 1 OR activo = 1)
           AND (@patron IS NULL OR nombre LIKE @patron ESCAPE '!')
        """;

    public Task<Grado?> ObtenerAsync(Guid id, CancellationToken ct) =>
        sesion.PrimeroAsync(Columnas + " WHERE id = @id", Convertir, ct, Parametros.Id("@id", id));

    public Task<Grado?> BuscarPorNombreAsync(string nombre, CancellationToken ct) =>
        sesion.PrimeroAsync(Columnas + " WHERE nombre_clave = @clave", Convertir, ct, Parametros.Texto("@clave", Textos.Clave(nombre)));

    public Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken ct) =>
        sesion.ExisteAsync("SELECT 1 FROM grados WHERE nombre_clave = @clave AND (@excluir IS NULL OR id <> @excluir)", ct,
            Parametros.Texto("@clave", Textos.Clave(nombre)), Parametros.IdOpcional("@excluir", excluirId));

    public async Task<Pagina<Grado>> BuscarAsync(FiltroTexto filtro, ConsultaPagina consulta, CancellationToken ct)
    {
        Parametro[] condiciones =
        [
            Parametros.Logico("@inactivos", filtro.IncluirInactivos),
            filtro.Texto is null ? Parametros.Texto("@patron", null) : Parametros.Patron("@patron", filtro.Texto)
        ];
        var total = await sesion.EnteroAsync("SELECT COUNT(*) FROM grados" + Filtro, ct, condiciones);
        var filas = await sesion.PaginaAsync(Columnas + Filtro + " ORDER BY orden, nombre_clave, id LIMIT @tamanio OFFSET @desde", Convertir,
            consulta.Tamanio, ct,
            [.. condiciones, Parametros.Entero("@tamanio", consulta.Tamanio), Parametros.Entero("@desde", consulta.Desplazamiento)]);
        return new Pagina<Grado>(filas, consulta.Numero, consulta.Tamanio, (int)total);
    }

    public Task AgregarAsync(Grado grado, CancellationToken ct) =>
        sesion.EjecutarAsync("""
            INSERT INTO grados (id, nombre, nombre_clave, orden, activo, version)
            VALUES (@id, @nombre, @clave, @orden, @activo, @version)
            """, ct, Datos(grado));

    public async Task<long> ActualizarAsync(Grado grado, CancellationToken ct)
    {
        var filas = await sesion.EjecutarAsync("""
            UPDATE grados SET nombre = @nombre, nombre_clave = @clave, orden = @orden, activo = @activo, version = version + 1
            WHERE id = @id AND version = @version
            """, ct, Datos(grado));
        await sesion.ExigirUnaFilaAsync(filas, "grados", grado.Id, ct);
        return grado.Version + 1;
    }

    public async Task RenombrarEnUsoAsync(string anterior, string nuevo, CancellationToken ct)
    {
        Parametro[] datos =
        [
            Parametros.Texto("@anterior", Textos.Clave(anterior)),
            Parametros.Texto("@nombre", nuevo),
            Parametros.Texto("@clave", Textos.Clave(nuevo))
        ];
        await sesion.EjecutarAsync("""
            UPDATE secciones SET grado = @nombre, grado_clave = @clave, version = version + 1 WHERE grado_clave = @anterior
            """, ct, datos);
        await sesion.EjecutarAsync("""
            UPDATE plan_materias SET grado = @nombre, grado_clave = @clave, version = version + 1 WHERE grado_clave = @anterior
            """, ct, datos);
    }

    private static Parametro[] Datos(Grado grado) =>
    [
        Parametros.Id("@id", grado.Id),
        Parametros.Texto("@nombre", grado.Nombre),
        Parametros.Texto("@clave", Textos.Clave(grado.Nombre)),
        Parametros.Entero("@orden", grado.Orden),
        Parametros.Logico("@activo", grado.Activo),
        Parametros.Largo("@version", grado.Version)
    ];

    private static Grado Convertir(MySqlDataReader fila) =>
        new(fila.Id(0), fila.GetString(1), fila.GetInt16(2), fila.Logico(3), fila.GetInt64(4));
}
