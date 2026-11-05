using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Application.Common.Consultas;
using Clarisa.Domain.Entities;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

internal sealed class RepositorioCuentasMariaDb(SesionMariaDb sesion) : IRepositorioCuentas
{
    internal const string Columnas = "id, usuario, nombre, perfil, docente_id, huella_clave, activo, version";

    private const string Filtro = """
         WHERE (@inactivos = 1 OR activo = 1)
           AND (@patron IS NULL OR CONVERT(usuario USING utf8mb4) COLLATE utf8mb4_unicode_520_ci LIKE @patron ESCAPE '!'
                OR nombre LIKE @patron ESCAPE '!')
        """;

    public Task<CuentaUsuario?> ObtenerAsync(Guid id, CancellationToken ct) =>
        sesion.PrimeroAsync($"SELECT {Columnas} FROM cuentas_usuario WHERE id = @id", fila => Convertir(fila, 0), ct,
            Parametros.Id("@id", id));

    public Task<CuentaUsuario?> ObtenerPorUsuarioAsync(string usuarioNormalizado, CancellationToken ct) =>
        sesion.PrimeroAsync($"SELECT {Columnas} FROM cuentas_usuario WHERE usuario = @usuario", fila => Convertir(fila, 0), ct,
            Parametros.Texto("@usuario", usuarioNormalizado));

    public Task<bool> ExisteUsuarioAsync(string usuarioNormalizado, Guid? excluirId, CancellationToken ct) =>
        sesion.ExisteAsync("SELECT 1 FROM cuentas_usuario WHERE usuario = @usuario AND (@excluir IS NULL OR id <> @excluir)", ct,
            Parametros.Texto("@usuario", usuarioNormalizado), Parametros.IdOpcional("@excluir", excluirId));

    public Task<bool> ExisteDocenteVinculadoAsync(Guid docenteId, Guid? excluirId, CancellationToken ct) =>
        sesion.ExisteAsync("SELECT 1 FROM cuentas_usuario WHERE docente_id = @docente AND (@excluir IS NULL OR id <> @excluir)", ct,
            Parametros.Id("@docente", docenteId), Parametros.IdOpcional("@excluir", excluirId));

    public async Task<int> ContarAdministradoresActivosAsync(CancellationToken ct) =>
        (int)await sesion.EnteroAsync("SELECT COUNT(*) FROM cuentas_usuario WHERE perfil = @perfil AND activo = 1", ct,
            Parametros.Entero("@perfil", (int)PerfilCuenta.Administracion));

    public Task<bool> ExisteAlgunaAsync(CancellationToken ct) => sesion.ExisteAsync("SELECT 1 FROM cuentas_usuario", ct);

    public async Task<Pagina<CuentaUsuario>> BuscarAsync(FiltroTexto filtro, ConsultaPagina consulta, CancellationToken ct)
    {
        Parametro[] condiciones =
        [
            Parametros.Logico("@inactivos", filtro.IncluirInactivos),
            filtro.Texto is null ? Parametros.Texto("@patron", null) : Parametros.Patron("@patron", filtro.Texto)
        ];
        var total = await sesion.EnteroAsync("SELECT COUNT(*) FROM cuentas_usuario" + Filtro, ct, condiciones);
        var filas = await sesion.PaginaAsync($"SELECT {Columnas} FROM cuentas_usuario" + Filtro
                + " ORDER BY nombre, usuario, id LIMIT @tamanio OFFSET @desde",
            fila => Convertir(fila, 0), consulta.Tamanio, ct,
            [.. condiciones, Parametros.Entero("@tamanio", consulta.Tamanio), Parametros.Entero("@desde", consulta.Desplazamiento)]);
        return new Pagina<CuentaUsuario>(filas, consulta.Numero, consulta.Tamanio, (int)total);
    }

    public Task AgregarAsync(CuentaUsuario cuenta, CancellationToken ct) =>
        sesion.EjecutarAsync("""
            INSERT INTO cuentas_usuario (id, usuario, nombre, perfil, docente_id, huella_clave, activo, version)
            VALUES (@id, @usuario, @nombre, @perfil, @docente, @huella, @activo, @version)
            """, ct, Datos(cuenta));

    public async Task<long> ActualizarAsync(CuentaUsuario cuenta, CancellationToken ct)
    {
        var filas = await sesion.EjecutarAsync("""
            UPDATE cuentas_usuario SET usuario = @usuario, nombre = @nombre, perfil = @perfil, docente_id = @docente,
                huella_clave = @huella, activo = @activo, version = version + 1
            WHERE id = @id AND version = @version
            """, ct, Datos(cuenta));
        await sesion.ExigirUnaFilaAsync(filas, "cuentas_usuario", cuenta.Id, ct);
        return cuenta.Version + 1;
    }

    private static Parametro[] Datos(CuentaUsuario cuenta) =>
    [
        Parametros.Id("@id", cuenta.Id),
        Parametros.Texto("@usuario", cuenta.Usuario),
        Parametros.Texto("@nombre", cuenta.Nombre),
        Parametros.Entero("@perfil", (int)cuenta.Perfil),
        Parametros.IdOpcional("@docente", cuenta.DocenteId),
        Parametros.Texto("@huella", cuenta.HuellaClave),
        Parametros.Logico("@activo", cuenta.Activo),
        Parametros.Largo("@version", cuenta.Version)
    ];

    // Desde la columna «inicio»: la consulta de sesiones lee la cuenta en la misma fila que la sesión.
    internal static CuentaUsuario Convertir(MySqlDataReader fila, int inicio) =>
        new(fila.Id(inicio), fila.GetString(inicio + 1), fila.GetString(inicio + 2), fila.Enumerado<PerfilCuenta>(inicio + 3),
            fila.IdOpcional(inicio + 4), fila.GetString(inicio + 5), fila.Logico(inicio + 6), fila.GetInt64(inicio + 7));
}
