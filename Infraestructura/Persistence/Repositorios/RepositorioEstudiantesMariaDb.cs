using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Consultas;
using Clarisa.Domain.Entities;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

internal sealed class RepositorioEstudiantesMariaDb(SesionMariaDb sesion) : IRepositorioEstudiantes
{
    private const string Columnas = "SELECT id, identificacion, nombres, apellidos, fecha_nacimiento, activo, version FROM estudiantes";

    // La cédula se guarda en ASCII binario; se convierte para buscar sin distinguir mayúsculas como en nombres y apellidos.
    private const string Filtro = """
         WHERE (@inactivos = 1 OR activo = 1)
           AND (@patron IS NULL OR CONVERT(identificacion USING utf8mb4) COLLATE utf8mb4_unicode_520_ci LIKE @patron ESCAPE '!'
                OR nombres LIKE @patron ESCAPE '!' OR apellidos LIKE @patron ESCAPE '!')
        """;

    public Task<Estudiante?> ObtenerAsync(Guid id, CancellationToken ct) =>
        sesion.PrimeroAsync(Columnas + " WHERE id = @id", Convertir, ct, Parametros.Id("@id", id));

    public Task<IReadOnlyList<Estudiante>> ObtenerVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        ConsultaPorIds.ListarAsync(sesion, Columnas + " WHERE id IN ({0})", ids, Convertir, ct);

    public Task<bool> ExisteIdentificacionAsync(string identificacion, Guid? excluirId, CancellationToken ct) =>
        sesion.ExisteAsync("SELECT 1 FROM estudiantes WHERE identificacion = @identificacion AND (@excluir IS NULL OR id <> @excluir)",
            ct, Parametros.Texto("@identificacion", identificacion), Parametros.IdOpcional("@excluir", excluirId));

    public async Task<Pagina<Estudiante>> BuscarAsync(FiltroTexto filtro, ConsultaPagina consulta, CancellationToken ct)
    {
        Parametro[] condiciones =
        [
            Parametros.Logico("@inactivos", filtro.IncluirInactivos),
            filtro.Texto is null ? Parametros.Texto("@patron", null) : Parametros.Patron("@patron", filtro.Texto)
        ];
        var total = await sesion.EnteroAsync("SELECT COUNT(*) FROM estudiantes" + Filtro, ct, condiciones);
        var filas = await sesion.PaginaAsync(Columnas + Filtro + " ORDER BY apellidos, nombres, id LIMIT @tamanio OFFSET @desde",
            Convertir, consulta.Tamanio, ct,
            [.. condiciones, Parametros.Entero("@tamanio", consulta.Tamanio), Parametros.Entero("@desde", consulta.Desplazamiento)]);
        return new Pagina<Estudiante>(filas, consulta.Numero, consulta.Tamanio, (int)total);
    }

    public Task AgregarAsync(Estudiante estudiante, CancellationToken ct) =>
        sesion.EjecutarAsync("""
            INSERT INTO estudiantes (id, identificacion, nombres, apellidos, fecha_nacimiento, activo, version)
            VALUES (@id, @identificacion, @nombres, @apellidos, @nacimiento, @activo, @version)
            """, ct, Datos(estudiante));

    public async Task<long> ActualizarAsync(Estudiante estudiante, CancellationToken ct)
    {
        var filas = await sesion.EjecutarAsync("""
            UPDATE estudiantes SET identificacion = @identificacion, nombres = @nombres, apellidos = @apellidos,
                fecha_nacimiento = @nacimiento, activo = @activo, version = version + 1
            WHERE id = @id AND version = @version
            """, ct, Datos(estudiante));
        await sesion.ExigirUnaFilaAsync(filas, "estudiantes", estudiante.Id, ct);
        return estudiante.Version + 1;
    }

    private static Parametro[] Datos(Estudiante estudiante) =>
    [
        Parametros.Id("@id", estudiante.Id),
        Parametros.Texto("@identificacion", estudiante.Identificacion),
        Parametros.Texto("@nombres", estudiante.Nombres),
        Parametros.Texto("@apellidos", estudiante.Apellidos),
        Parametros.FechaOpcional("@nacimiento", estudiante.FechaNacimiento),
        Parametros.Logico("@activo", estudiante.Activo),
        Parametros.Largo("@version", estudiante.Version)
    ];

    private static Estudiante Convertir(MySqlDataReader fila) =>
        new(fila.Id(0), fila.TextoOpcional(1), fila.GetString(2), fila.GetString(3), fila.FechaOpcional(4), fila.Logico(5),
            fila.GetInt64(6));
}
