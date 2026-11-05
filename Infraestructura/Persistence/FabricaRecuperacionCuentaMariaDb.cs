using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Infrastructure.Configuration;
using Clarisa.Infrastructure.Persistence.Repositorios;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence;

public sealed class FabricaRecuperacionCuentaMariaDb : IFabricaRecuperacionCuenta
{
    private readonly AperturaUnidadMariaDb apertura;
    private readonly string usuarioOperador;

    public FabricaRecuperacionCuentaMariaDb(ConfiguracionMariaDb aplicacion, ConfiguracionMariaDb operador)
    {
        aplicacion.Validar();
        operador.Validar();
        if (string.Equals(aplicacion.Usuario, operador.Usuario, StringComparison.OrdinalIgnoreCase)
            || aplicacion.Servidor != operador.Servidor || aplicacion.Puerto != operador.Puerto || aplicacion.Base != operador.Base)
            throw new ArgumentException("La recuperación requiere un operador distinto de la aplicación, sobre la misma base local.");
        usuarioOperador = operador.Usuario;
        apertura = new(operador);
    }

    public async Task<IUnidadRecuperacionCuenta> IniciarAsync(ModoUnidadTrabajo modo, CancellationToken ct) =>
        await apertura.AbrirAsync(modo, (conexion, transaccion, tipo, segundos) =>
            new UnidadRecuperacionCuenta(conexion, transaccion, tipo, segundos, usuarioOperador), ct);

    private sealed class UnidadRecuperacionCuenta : UnidadMariaDb, IUnidadRecuperacionCuenta
    {
        private readonly string usuarioOperador;
        public UnidadRecuperacionCuenta(MySqlConnection conexion, MySqlTransaction transaccion, ModoUnidadTrabajo modo,
            int segundos, string usuarioOperador) : base(conexion, transaccion, modo, segundos)
        {
            this.usuarioOperador = usuarioOperador;
            Cuentas = new RepositorioCuentasMariaDb(Sesion);
            Sesiones = new RepositorioSesionesMariaDb(Sesion);
            Auditoria = new RegistroAuditoriaCuentasMariaDb(Sesion);
            Docentes = new RepositorioDocentesMariaDb(Sesion);
        }
        public IRepositorioCuentas Cuentas { get; }
        public IRepositorioSesiones Sesiones { get; }
        public IRegistroAuditoriaCuentas Auditoria { get; }
        public IRepositorioDocentes Docentes { get; }
        public Task RegistrarRecuperacionAsync(EventoRecuperacionCuenta evento, CancellationToken ct)
        {
            if (evento.Operador.UsuarioBase != usuarioOperador) throw new InvalidOperationException("El operador no coincide con la conexión de recuperación.");
            return Sesion.EjecutarAsync("""
                INSERT INTO recuperaciones_cuenta (id, cuenta_id, version_resultante, identidad_windows, usuario_base, motivo, ocurrido_utc_ticks)
                VALUES (@id, @cuenta, @version, @windows, @operador, @motivo, @ocurrido)
                """, ct, Parametros.Id("@id", evento.Id), Parametros.Id("@cuenta", evento.CuentaId),
                Parametros.Largo("@version", evento.VersionResultante), Parametros.Texto("@windows", evento.Operador.IdentidadWindows),
                Parametros.Texto("@operador", usuarioOperador), Parametros.Texto("@motivo", evento.Motivo), Parametros.Instante("@ocurrido", evento.Ocurrido));
        }
    }
}
