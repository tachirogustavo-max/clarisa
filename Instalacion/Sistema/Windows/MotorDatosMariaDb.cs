using System.Diagnostics;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace Clarisa.Instalacion.Sistema.Windows;

// Los nombres de base y usuario son constantes validadas (los identificadores no admiten parámetros); toda clave va como parámetro.
internal sealed partial class MotorDatosMariaDb(IProcesos procesos) : IMotorDatos
{
    private static readonly string[] Anfitriones = ["localhost", "127.0.0.1"];
    private static readonly TimeSpan LimiteInicial = TimeSpan.FromMinutes(3);

    public async Task InicializarAsync(string ejecutableInstalar, string ejecutableMotor, string directorioDatos, string claveAdministracion,
        CancellationToken ct)
    {
        var instalacion = await procesos.EjecutarAsync(new SolicitudProceso(ejecutableInstalar,
            ["--datadir=" + directorioDatos, "--skip-networking", "--silent"], LimiteInicial), ct);
        if (instalacion.Codigo != 0 || instalacion.VencioLimite || !Directory.Exists(Path.Combine(directorioDatos, "mysql")))
            throw new Ejecucion.FalloInstalacionException($"mariadb-install-db no creó los datos (código {instalacion.Codigo}).");

        // Primer arranque sin red y con canalización de nombre aleatorio: nadie puede conectarse por TCP con la clave vacía inicial.
        var canal = "clarisa-" + Guid.NewGuid().ToString("N");
        var inicio = new ProcessStartInfo(ejecutableMotor) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argumento in new[] { "--no-defaults", "--datadir=" + directorioDatos, "--skip-networking", "--named-pipe",
                     "--socket=" + canal, "--skip-log-bin", "--innodb-buffer-pool-size=32M" })
            inicio.ArgumentList.Add(argumento);
        using var proceso = Process.Start(inicio) ?? throw new Ejecucion.FalloInstalacionException("No se pudo iniciar el motor.");
        proceso.OutputDataReceived += (_, _) => { };
        proceso.ErrorDataReceived += (_, _) => { };
        proceso.BeginOutputReadLine();
        proceso.BeginErrorReadLine();
        var apagado = false;
        try
        {
            // Canalización local de nombre aleatorio: TLS no aporta nada aquí y MariaDB 11.4 no lo completa por canalización.
            var cadena = new MySqlConnectionStringBuilder
            {
                ConnectionProtocol = MySqlConnectionProtocol.NamedPipe, PipeName = canal, Server = ".", UserID = "root", Password = "",
                ConnectionTimeout = 5, Pooling = false, SslMode = MySqlSslMode.None
            }.ConnectionString;
            await using var conexion = await ConectarConEsperaAsync(cadena, proceso, ct);
            var raices = await ListaAsync(conexion, "SELECT User, Host FROM mysql.user WHERE User IN ('root', '')", ct);
            foreach (var (usuario, anfitrion) in raices)
            {
                if (usuario == "root" && Anfitriones.Contains(anfitrion))
                    await EjecutarAsync(conexion, $"ALTER USER 'root'@'{anfitrion}' IDENTIFIED BY @clave", ct, ("@clave", claveAdministracion));
                else if (Anfitrion().IsMatch(anfitrion))
                    await EjecutarAsync(conexion, $"DROP USER '{usuario}'@'{anfitrion}'", ct);
                else
                    throw new Ejecucion.FalloInstalacionException("El motor recién creado tiene una cuenta inesperada.");
            }
            // Con skip-name-resolve, una conexión TCP desde 127.0.0.1 no coincide con 'localhost': la administración necesita esa cuenta.
            if (!raices.Contains(("root", "127.0.0.1")))
            {
                await EjecutarAsync(conexion, "CREATE USER 'root'@'127.0.0.1' IDENTIFIED BY @clave", ct, ("@clave", claveAdministracion));
                await EjecutarAsync(conexion, "GRANT ALL PRIVILEGES ON *.* TO 'root'@'127.0.0.1' WITH GRANT OPTION", ct);
            }
            await EjecutarAsync(conexion, "DROP DATABASE IF EXISTS test", ct);
            await RetirarPublicoAsync(conexion, ct);
            await EjecutarAsync(conexion, "SHUTDOWN", ct);
            apagado = true;
        }
        finally
        {
            // Sin apagado pedido el proceso no terminaría solo: se detiene de inmediato en vez de esperar el límite.
            using var espera = new CancellationTokenSource(apagado ? TimeSpan.FromSeconds(60) : TimeSpan.Zero);
            try
            {
                await proceso.WaitForExitAsync(espera.Token);
            }
            catch (OperationCanceledException)
            {
                proceso.Kill(entireProcessTree: true);
                await proceso.WaitForExitAsync(CancellationToken.None);
                if (apagado) throw new Ejecucion.FalloInstalacionException("El motor recién creado no se apagó a tiempo y se detuvo a la fuerza.");
            }
        }
    }

    public async Task<IdentidadMotor?> IdentificarAsync(int puerto, string claveAdministracion, CancellationToken ct)
    {
        try
        {
            await using var conexion = await AbrirAsync(puerto, "root", claveAdministracion, null, ct);
            await using var comando = new MySqlCommand("SELECT @@port, @@datadir, @@bind_address, VERSION()", conexion);
            await using var lector = await comando.ExecuteReaderAsync(ct);
            await lector.ReadAsync(ct);
            return new IdentidadMotor(lector.GetInt32(0), lector.GetString(1), lector.GetString(2), lector.GetString(3));
        }
        catch (MySqlException error) when (error.ErrorCode is MySqlErrorCode.UnableToConnectToHost or MySqlErrorCode.AccessDenied)
        {
            return null;
        }
    }

    public async Task AsegurarCuentasAsync(int puerto, string claveAdministracion, CuentasMotor cuentas, string? claveOperador, CancellationToken ct)
    {
        Exigir(cuentas);
        await using var conexion = await AbrirAsync(puerto, "root", claveAdministracion, null, ct);
        await RetirarPublicoAsync(conexion, ct);
        await EjecutarAsync(conexion, $"CREATE DATABASE IF NOT EXISTS `{cuentas.Base}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_520_ci", ct);
        foreach (var anfitrion in Anfitriones)
        {
            await AsegurarUsuarioAsync(conexion, cuentas.UsuarioAplicacion, anfitrion, cuentas.ClaveAplicacion, ct);
            await EjecutarAsync(conexion, $"GRANT SELECT, INSERT, UPDATE, DELETE ON `{cuentas.PrefijoBases}`.* TO '{cuentas.UsuarioAplicacion}'@'{anfitrion}'", ct);
            await AsegurarUsuarioAsync(conexion, cuentas.UsuarioMigracion, anfitrion, null, ct);
            await EjecutarAsync(conexion, $"GRANT ALL PRIVILEGES ON `{cuentas.PrefijoBases}`.* TO '{cuentas.UsuarioMigracion}'@'{anfitrion}'", ct);
            await AsegurarUsuarioAsync(conexion, cuentas.UsuarioOperador, anfitrion, claveOperador, ct);
            await EjecutarAsync(conexion, $"GRANT ALL PRIVILEGES ON `{cuentas.PrefijoBases}`.* TO '{cuentas.UsuarioOperador}'@'{anfitrion}'", ct);
            await EjecutarAsync(conexion, $"GRANT PROCESS, CONNECTION ADMIN ON *.* TO '{cuentas.UsuarioOperador}'@'{anfitrion}'", ct);
        }
    }

    public async Task<string?> VerificarCuentasAsync(int puerto, string claveAdministracion, CuentasMotor cuentas, CancellationToken ct)
    {
        Exigir(cuentas);
        await using (var raiz = await AbrirAsync(puerto, "root", claveAdministracion, null, ct))
        {
            var existentes = await ListaAsync(raiz, "SELECT User, Host FROM mysql.user", ct);
            foreach (var usuario in new[] { cuentas.UsuarioAplicacion, cuentas.UsuarioMigracion, cuentas.UsuarioOperador })
                if (!Anfitriones.All(a => existentes.Contains((usuario, a)))) return $"Falta la cuenta {usuario} del motor.";
            if (existentes.Any(e => e.Usuario.Length == 0)) return "El motor tiene cuentas anónimas.";
            if (existentes.Any(e => e.Usuario == "root" && !Anfitriones.Contains(e.Anfitrion))) return "La cuenta root acepta conexiones no locales.";
        }
        try
        {
            await using var aplicacion = await AbrirAsync(puerto, cuentas.UsuarioAplicacion, cuentas.ClaveAplicacion, cuentas.Base, ct);
            var permisos = (await ListaAsync(aplicacion, "SHOW GRANTS", ct)).Select(p => p.Usuario).ToList();
            var amplios = permisos.Where(p => !p.StartsWith("GRANT USAGE ON *.*", StringComparison.Ordinal)
                                              && !DatosSolamente().IsMatch(p)).ToList();
            if (!permisos.Any(p => DatosSolamente().IsMatch(p))) return "La cuenta de la aplicación no tiene permisos de datos sobre las bases del liceo.";
            var adicional = amplios.Select(p => p.Split(" IDENTIFIED BY ")[0]).FirstOrDefault();
            return adicional is null ? null : $"La cuenta de la aplicación tiene permisos distintos de SELECT/INSERT/UPDATE/DELETE: {adicional}";
        }
        catch (MySqlException error) when (error.ErrorCode is MySqlErrorCode.AccessDenied or MySqlErrorCode.UnknownDatabase)
        {
            return "La cuenta de la aplicación no entra a su base con la clave configurada.";
        }
    }

    public async Task FijarClaveMigracionAsync(int puerto, string claveAdministracion, string usuario, string clave, CancellationToken ct)
    {
        if (!Identificador().IsMatch(usuario)) throw new ArgumentException("Usuario no válido.", nameof(usuario));
        await using var conexion = await AbrirAsync(puerto, "root", claveAdministracion, null, ct);
        foreach (var anfitrion in Anfitriones)
            await EjecutarAsync(conexion, $"ALTER USER '{usuario}'@'{anfitrion}' IDENTIFIED BY @clave", ct, ("@clave", clave));
    }

    // MariaDB 11 concede a PUBLIC (toda cuenta) permisos amplios sobre `test` y `test\_%`; la aplicación no debe heredarlos.
    private static async Task RetirarPublicoAsync(MySqlConnection conexion, CancellationToken ct)
    {
        List<(string Usuario, string Anfitrion)> concesiones;
        try
        {
            concesiones = await ListaAsync(conexion, "SHOW GRANTS FOR PUBLIC", ct);
        }
        catch (MySqlException error) when (error.ErrorCode == MySqlErrorCode.NonExistingGrant)
        {
            return;
        }
        foreach (var (linea, _) in concesiones)
        {
            if (linea.StartsWith("GRANT USAGE ON *.*", StringComparison.Ordinal)) continue;
            var base_ = ConcesionPublica().Match(linea);
            if (!base_.Success) throw new Ejecucion.FalloInstalacionException("El rol PUBLIC del motor tiene una concesión inesperada; no se continúa.");
            await EjecutarAsync(conexion, $"REVOKE ALL PRIVILEGES ON `{base_.Groups[1].Value}`.* FROM PUBLIC", ct);
        }
    }

    private static async Task AsegurarUsuarioAsync(MySqlConnection conexion, string usuario, string anfitrion, string? clave, CancellationToken ct)
    {
        var aleatoria = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(30));
        await EjecutarAsync(conexion, $"CREATE USER IF NOT EXISTS '{usuario}'@'{anfitrion}' IDENTIFIED BY @clave", ct, ("@clave", clave ?? aleatoria));
        if (clave is not null) await EjecutarAsync(conexion, $"ALTER USER '{usuario}'@'{anfitrion}' IDENTIFIED BY @clave", ct, ("@clave", clave));
    }

    private static async Task<MySqlConnection> AbrirAsync(int puerto, string usuario, string clave, string? baseDatos, CancellationToken ct)
    {
        var conexion = new MySqlConnection(new MySqlConnectionStringBuilder
        {
            Server = "127.0.0.1", Port = (uint)puerto, UserID = usuario, Password = clave, Database = baseDatos ?? "", ConnectionTimeout = 5,
            Pooling = false, SslMode = MySqlSslMode.None, AllowPublicKeyRetrieval = false, AllowLoadLocalInfile = false
        }.ConnectionString);
        try
        {
            await conexion.OpenAsync(ct);
            return conexion;
        }
        catch
        {
            await conexion.DisposeAsync();
            throw;
        }
    }

    private static async Task<MySqlConnection> ConectarConEsperaAsync(string cadena, Process proceso, CancellationToken ct)
    {
        for (var intento = 0; ; intento++)
        {
            var conexion = new MySqlConnection(cadena);
            try
            {
                await conexion.OpenAsync(ct);
                return conexion;
            }
            catch (MySqlException) when (intento < 60 && !proceso.HasExited)
            {
                await conexion.DisposeAsync();
                await Task.Delay(500, ct);
            }
        }
    }

    private static async Task<List<(string Usuario, string Anfitrion)>> ListaAsync(MySqlConnection conexion, string sql, CancellationToken ct)
    {
        await using var comando = new MySqlCommand(sql, conexion);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        var filas = new List<(string, string)>();
        while (await lector.ReadAsync(ct)) filas.Add((lector.GetString(0), lector.FieldCount > 1 ? lector.GetString(1) : ""));
        return filas;
    }

    private static async Task EjecutarAsync(MySqlConnection conexion, string sql, CancellationToken ct, params (string Nombre, string Valor)[] parametros)
    {
        await using var comando = new MySqlCommand(sql, conexion);
        foreach (var (nombre, valor) in parametros) comando.Parameters.AddWithValue(nombre, valor);
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static void Exigir(CuentasMotor cuentas)
    {
        if (!Identificador().IsMatch(cuentas.Base) || cuentas.PrefijoBases != "liceo\\_%"
            || new[] { cuentas.UsuarioAplicacion, cuentas.UsuarioMigracion, cuentas.UsuarioOperador }.Any(u => !Identificador().IsMatch(u)))
            throw new ArgumentException("Nombres del motor no válidos.");
    }

    [GeneratedRegex("^[A-Za-z0-9._:-]{1,255}$")]
    private static partial Regex Anfitrion();

    [GeneratedRegex("^[a-z0-9_]{1,64}$")]
    private static partial Regex Identificador();

    [GeneratedRegex(@"^GRANT SELECT, INSERT, UPDATE, DELETE ON `liceo\\_%`\.\* TO ")]
    private static partial Regex DatosSolamente();

    [GeneratedRegex(@"^GRANT .+ ON `([A-Za-z0-9_\\%]{1,64})`\.\* TO PUBLIC$")]
    private static partial Regex ConcesionPublica();
}
