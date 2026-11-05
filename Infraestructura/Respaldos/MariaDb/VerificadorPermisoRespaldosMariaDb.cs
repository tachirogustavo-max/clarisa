using Clarisa.Application.Common.Errores;
using Clarisa.Application.Features.Respaldos;
using Clarisa.Domain.Entities;
using Clarisa.Infrastructure.Configuration;
using MySqlConnector;

namespace Clarisa.Infrastructure.Respaldos.MariaDb;

public sealed class VerificadorPermisoRespaldosMariaDb(ConfiguracionMariaDb datos) : IVerificadorPermisoRespaldos
{
    public async Task<bool> EsAdministracionActivaAsync(Guid cuentaId, CancellationToken ct)
    {
        if (cuentaId == Guid.Empty) return false;
        try
        {
            await using var conexion = new MySqlConnection(datos.CadenaConexion());
            await conexion.OpenAsync(ct);
            return await ConexionRespaldo.EscalarAsync(conexion,
                "SELECT COUNT(*) FROM cuentas_usuario WHERE id = @id AND activo = 1 AND perfil = @perfil", ct,
                ("@id", cuentaId), ("@perfil", (int)PerfilCuenta.Administracion)) == 1;
        }
        catch (Exception error) when (!ct.IsCancellationRequested && error is MySqlException or InvalidOperationException
                                          or IOException or TimeoutException)
        {
            throw Error.AlmacenNoDisponible(error);
        }
    }
}
