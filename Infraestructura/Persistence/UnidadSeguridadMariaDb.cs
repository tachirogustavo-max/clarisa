using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Infrastructure.Persistence.Repositorios;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence;

internal sealed class UnidadSeguridadMariaDb : UnidadMariaDb, IUnidadSeguridad
{
    public UnidadSeguridadMariaDb(MySqlConnection conexion, MySqlTransaction transaccion, ModoUnidadTrabajo modo, int segundosComando)
        : base(conexion, transaccion, modo, segundosComando)
    {
        Cuentas = new RepositorioCuentasMariaDb(Sesion);
        Sesiones = new RepositorioSesionesMariaDb(Sesion);
        Auditoria = new RegistroAuditoriaCuentasMariaDb(Sesion);
        Docentes = new RepositorioDocentesMariaDb(Sesion);
    }

    public IRepositorioCuentas Cuentas { get; }
    public IRepositorioSesiones Sesiones { get; }
    public IRegistroAuditoriaCuentas Auditoria { get; }
    public IRepositorioDocentes Docentes { get; }
}
