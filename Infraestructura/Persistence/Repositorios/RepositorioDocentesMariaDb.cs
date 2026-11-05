using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Consultas;
using Clarisa.Domain.Entities;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

internal sealed class RepositorioDocentesMariaDb(SesionMariaDb sesion) : IRepositorioDocentes
{
    private const string Columnas = "SELECT id, identificacion, nombres, apellidos, correo, activo, version FROM docentes";

    private const string Filtro = """
         WHERE (@inactivos = 1 OR activo = 1)
           AND (@patron IS NULL OR CONVERT(identificacion USING utf8mb4) COLLATE utf8mb4_unicode_520_ci LIKE @patron ESCAPE '!'
                OR nombres LIKE @patron ESCAPE '!' OR apellidos LIKE @patron ESCAPE '!')
        """;

    public Task<Docente?> ObtenerAsync(Guid id, CancellationToken ct) =>
        sesion.PrimeroAsync(Columnas + " WHERE id = @id", Convertir, ct, Parametros.Id("@id", id));

    public Task<bool> ExisteIdentificacionAsync(string identificacion, Guid? excluirId, CancellationToken ct) =>
        sesion.ExisteAsync("SELECT 1 FROM docentes WHERE identificacion = @identificacion AND (@excluir IS NULL OR id <> @excluir)",
            ct, Parametros.Texto("@identificacion", identificacion), Parametros.IdOpcional("@excluir", excluirId));

    public async Task<Pagina<Docente>> BuscarAsync(FiltroTexto filtro, ConsultaPagina consulta, CancellationToken ct)
    {
        Parametro[] condiciones =
        [
            Parametros.Logico("@inactivos", filtro.IncluirInactivos),
            filtro.Texto is null ? Parametros.Texto("@patron", null) : Parametros.Patron("@patron", filtro.Texto)
        ];
        var total = await sesion.EnteroAsync("SELECT COUNT(*) FROM docentes" + Filtro, ct, condiciones);
        var filas = await sesion.PaginaAsync(Columnas + Filtro + " ORDER BY apellidos, nombres, id LIMIT @tamanio OFFSET @desde",
            Convertir, consulta.Tamanio, ct,
            [.. condiciones, Parametros.Entero("@tamanio", consulta.Tamanio), Parametros.Entero("@desde", consulta.Desplazamiento)]);
        return new Pagina<Docente>(filas, consulta.Numero, consulta.Tamanio, (int)total);
    }

    public Task AgregarAsync(Docente docente, CancellationToken ct) =>
        sesion.EjecutarAsync("""
            INSERT INTO docentes (id, identificacion, nombres, apellidos, correo, activo, version)
            VALUES (@id, @identificacion, @nombres, @apellidos, @correo, @activo, @version)
            """, ct, Datos(docente));

    public async Task<long> ActualizarAsync(Docente docente, CancellationToken ct)
    {
        var filas = await sesion.EjecutarAsync("""
            UPDATE docentes SET identificacion = @identificacion, nombres = @nombres, apellidos = @apellidos, correo = @correo,
                activo = @activo, version = version + 1
            WHERE id = @id AND version = @version
            """, ct, Datos(docente));
        await sesion.ExigirUnaFilaAsync(filas, "docentes", docente.Id, ct);
        return docente.Version + 1;
    }

    private static Parametro[] Datos(Docente docente) =>
    [
        Parametros.Id("@id", docente.Id),
        Parametros.Texto("@identificacion", docente.Identificacion),
        Parametros.Texto("@nombres", docente.Nombres),
        Parametros.Texto("@apellidos", docente.Apellidos),
        Parametros.Texto("@correo", docente.Correo),
        Parametros.Logico("@activo", docente.Activo),
        Parametros.Largo("@version", docente.Version)
    ];

    private static Docente Convertir(MySqlDataReader fila) =>
        new(fila.Id(0), fila.GetString(1), fila.GetString(2), fila.GetString(3), fila.TextoOpcional(4), fila.Logico(5), fila.GetInt64(6));
}
