using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Consultas;
using Clarisa.Domain.Entities;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

internal sealed class RepositorioMateriasMariaDb(SesionMariaDb sesion) : IRepositorioMaterias
{
    private const string Columnas = "SELECT id, codigo, nombre, activa, version FROM materias";

    private const string Filtro = """
         WHERE (@inactivas = 1 OR activa = 1)
           AND (@patron IS NULL OR nombre LIKE @patron ESCAPE '!'
                OR CONVERT(codigo USING utf8mb4) COLLATE utf8mb4_unicode_520_ci LIKE @patron ESCAPE '!')
        """;

    public Task<Materia?> ObtenerAsync(Guid id, CancellationToken ct) =>
        sesion.PrimeroAsync(Columnas + " WHERE id = @id", Convertir, ct, Parametros.Id("@id", id));

    public Task<IReadOnlyList<Materia>> ObtenerVariasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        ConsultaPorIds.ListarAsync(sesion, Columnas + " WHERE id IN ({0})", ids, Convertir, ct);

    public Task<bool> ExisteCodigoAsync(string codigo, Guid? excluirId, CancellationToken ct) =>
        sesion.ExisteAsync("SELECT 1 FROM materias WHERE codigo = @codigo AND (@excluir IS NULL OR id <> @excluir)", ct,
            Parametros.Texto("@codigo", codigo), Parametros.IdOpcional("@excluir", excluirId));

    public async Task<Pagina<Materia>> BuscarAsync(FiltroTexto filtro, ConsultaPagina consulta, CancellationToken ct)
    {
        Parametro[] condiciones =
        [
            Parametros.Logico("@inactivas", filtro.IncluirInactivos),
            filtro.Texto is null ? Parametros.Texto("@patron", null) : Parametros.Patron("@patron", filtro.Texto)
        ];
        var total = await sesion.EnteroAsync("SELECT COUNT(*) FROM materias" + Filtro, ct, condiciones);
        var filas = await sesion.PaginaAsync(Columnas + Filtro + " ORDER BY codigo, id LIMIT @tamanio OFFSET @desde", Convertir,
            consulta.Tamanio, ct,
            [.. condiciones, Parametros.Entero("@tamanio", consulta.Tamanio), Parametros.Entero("@desde", consulta.Desplazamiento)]);
        return new Pagina<Materia>(filas, consulta.Numero, consulta.Tamanio, (int)total);
    }

    public Task AgregarAsync(Materia materia, CancellationToken ct) =>
        sesion.EjecutarAsync("INSERT INTO materias (id, codigo, nombre, activa, version) VALUES (@id, @codigo, @nombre, @activa, @version)",
            ct, Datos(materia));

    public async Task<long> ActualizarAsync(Materia materia, CancellationToken ct)
    {
        var filas = await sesion.EjecutarAsync("""
            UPDATE materias SET codigo = @codigo, nombre = @nombre, activa = @activa, version = version + 1
            WHERE id = @id AND version = @version
            """, ct, Datos(materia));
        await sesion.ExigirUnaFilaAsync(filas, "materias", materia.Id, ct);
        return materia.Version + 1;
    }

    private static Parametro[] Datos(Materia materia) =>
    [
        Parametros.Id("@id", materia.Id),
        Parametros.Texto("@codigo", materia.Codigo),
        Parametros.Texto("@nombre", materia.Nombre),
        Parametros.Logico("@activa", materia.Activa),
        Parametros.Largo("@version", materia.Version)
    ];

    private static Materia Convertir(MySqlDataReader fila) =>
        new(fila.Id(0), fila.GetString(1), fila.GetString(2), fila.Logico(3), fila.GetInt64(4));
}
