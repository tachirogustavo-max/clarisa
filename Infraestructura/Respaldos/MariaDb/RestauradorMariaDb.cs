using System.Security.Cryptography;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Features.Respaldos;
using Clarisa.Infrastructure.Configuration;
using Clarisa.Infrastructure.Persistence.Esquema;
using Clarisa.Infrastructure.Respaldos.Claves;
using Clarisa.Infrastructure.Respaldos.Formato;
using MySqlConnector;

namespace Clarisa.Infrastructure.Respaldos.MariaDb;

// Nunca toca la base activa: crea una base nueva con nombre propio, la migra, carga y verifica. Ante cualquier fallo elimina solo esa
// base recién creada. No ejecuta SQL procedente del archivo.
public sealed class RestauradorMariaDb(ConfiguracionMariaDb datos, ConfiguracionRespaldos configuracion, AlmacenClaveRespaldos claves)
    : IRecuperadorCopias
{
    private LimitesPaquete Limites => GeneradorCopiasMariaDb.LimitesDe(configuracion);

    public async Task<ResumenCopia> ValidarAsync(string archivo, OrigenClave origen, CancellationToken ct)
    {
        using var clave = claves.Obtener(origen);
        var plano = Descifrar(archivo, clave);
        try
        {
            using var paquete = LectorPaquete.Abrir(plano, Limites);
            await paquete.ValidarTodoAsync(ct);
            return paquete.Manifiesto.Resumir();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plano);
        }
    }

    public async Task<BaseRecuperada> RecuperarEnBaseNuevaAsync(string archivo, OrigenClave origen, CredencialOperador operador,
        CancellationToken ct)
    {
        using var clave = claves.Obtener(origen);
        var plano = Descifrar(archivo, clave);
        try
        {
            ResumenCopia resumen;
            using (var validacion = LectorPaquete.Abrir(plano, Limites))
            {
                await validacion.ValidarTodoAsync(ct);
                if (!validacion.Manifiesto.Migraciones.SequenceEqual(LecturaTablasMariaDb.MigracionesActuales()))
                    throw ErroresRespaldo.Incompatible();
                resumen = validacion.Manifiesto.Resumir();
            }
            var nombre = configuracion.PrefijoBaseRecuperada + Guid.NewGuid().ToString("N");
            var comoOperador = ConexionRespaldo.ComoOperador(datos, nombre, operador);
            await TraducirAsync(async () =>
            {
                await CrearBaseAsync(comoOperador, ct);
                return true;
            });
            try
            {
                await TraducirAsync(async () =>
                {
                    var migrador = new MigradorEsquemaMariaDb(comoOperador);
                    await migrador.AplicarAsync(ct);
                    await using var conexion = await ConexionRespaldo.AbrirAsync(comoOperador, sinBase: false, ct);
                    using (var paquete = LectorPaquete.Abrir(plano, Limites)) await CargaTablasMariaDb.CargarAsync(conexion, paquete, ct);
                    await ExigirContenidoAsync(conexion, resumen.HuellaContenido, ct);
                    return true;
                });
                await ExigirAccesoAplicacionAsync(nombre, ct);
                return new BaseRecuperada(nombre, resumen);
            }
            catch
            {
                await EliminarBaseCreadaAsync(comoOperador);
                throw;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plano);
        }
    }

    public async Task VerificarContenidoAsync(string baseDatos, string huellaContenido, CredencialOperador operador, CancellationToken ct)
    {
        ExigirNombre(baseDatos);
        await TraducirAsync(async () =>
        {
            var comoOperador = ConexionRespaldo.ComoOperador(datos, baseDatos, operador);
            await new MigradorEsquemaMariaDb(comoOperador).VerificarAsync(ct);
            await using var conexion = await ConexionRespaldo.AbrirAsync(comoOperador, sinBase: false, ct);
            await ExigirContenidoAsync(conexion, huellaContenido, ct);
            return true;
        });
        await ExigirAccesoAplicacionAsync(baseDatos, ct);
    }

    public async Task VerificarEsquemaAsync(string baseDatos, CredencialOperador operador, CancellationToken ct)
    {
        ExigirNombre(baseDatos);
        await TraducirAsync(async () =>
        {
            await new MigradorEsquemaMariaDb(ConexionRespaldo.ComoOperador(datos, baseDatos, operador)).VerificarAsync(ct);
            return true;
        });
        await ExigirAccesoAplicacionAsync(baseDatos, ct);
    }

    private byte[] Descifrar(string archivo, ClaveRespaldo clave)
    {
        try
        {
            using var flujo = new FileStream(archivo, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);
            return SobreCifrado.Leer(flujo, clave, Limites.MaximoCifrado);
        }
        catch (FileNotFoundException)
        {
            throw Error.NoEncontrado("No se encontró el archivo de la copia indicado.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw Error.AlmacenNoDisponible(error);
        }
    }

    // Esquema y migraciones exactos, sin sesiones, y las mismas huellas por tabla que la copia al releer la base ya confirmada.
    private static async Task ExigirContenidoAsync(MySqlConnection conexion, string huellaContenido, CancellationToken ct)
    {
        await LecturaTablasMariaDb.ExigirEsquemaAsync(conexion, ct);
        if (await ConexionRespaldo.EscalarAsync(conexion, "SELECT COUNT(*) FROM sesiones_usuario", ct) != 0)
            throw Error.EstadoNoPermitido("La base recuperada contiene sesiones; no se activa.");
        if (await LecturaTablasMariaDb.HuellaContenidoAsync(conexion, ct) != huellaContenido)
            throw Error.EstadoNoPermitido("La base recuperada no reproduce exactamente el contenido de la copia. No se activa.");
    }

    // El servidor usará el usuario de la aplicación: se comprueba con él, no con la credencial del operador.
    private async Task ExigirAccesoAplicacionAsync(string baseDatos, CancellationToken ct)
    {
        try
        {
            await new MigradorEsquemaMariaDb(datos with { Base = baseDatos }).VerificarAsync(ct);
        }
        catch (EsquemaNoDisponibleException error)
        {
            throw new ErrorAplicacionException(TipoError.EstadoNoPermitido,
                "El usuario de la aplicación no puede usar la base recuperada. Revisa sus permisos sobre las bases de recuperación.",
                causa: error);
        }
    }

    private static async Task CrearBaseAsync(ConfiguracionMariaDb comoOperador, CancellationToken ct)
    {
        ExigirNombre(comoOperador.Base);
        await using var conexion = await ConexionRespaldo.AbrirAsync(comoOperador, sinBase: true, ct);
        await ConexionRespaldo.EjecutarAsync(conexion,
            $"CREATE DATABASE `{comoOperador.Base}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_520_ci", ct);
    }

    private async Task EliminarBaseCreadaAsync(ConfiguracionMariaDb comoOperador)
    {
        if (!comoOperador.Base.StartsWith(configuracion.PrefijoBaseRecuperada, StringComparison.Ordinal)) return;
        try
        {
            await using var conexion = await ConexionRespaldo.AbrirAsync(comoOperador, sinBase: true, CancellationToken.None);
            await ConexionRespaldo.EjecutarAsync(conexion, $"DROP DATABASE IF EXISTS `{comoOperador.Base}`", CancellationToken.None);
        }
        catch (MySqlException error)
        {
            throw new ErrorAplicacionException(TipoError.AlmacenNoDisponible,
                $"La recuperación falló y no se pudo eliminar la base nueva incompleta {comoOperador.Base}. La base activa no cambió; "
                + "elimínela manualmente.", causa: error);
        }
    }

    private static void ExigirNombre(string baseDatos)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(baseDatos, "^[a-z0-9_]{1,64}$"))
            throw Error.DatosInvalidos("Nombre de base no válido.", "Base");
    }

    private static async Task<T> TraducirAsync<T>(Func<Task<T>> accion)
    {
        try
        {
            return await accion();
        }
        catch (MySqlException error) when (error.ErrorCode is MySqlErrorCode.AccessDenied or MySqlErrorCode.DatabaseAccessDenied
                                               or MySqlErrorCode.TableAccessDenied or MySqlErrorCode.SpecifiedAccessDeniedError)
        {
            throw new ErrorAplicacionException(TipoError.AccesoDenegado,
                "La cuenta del operador no tiene permiso para crear o preparar la base de recuperación.", causa: error);
        }
        catch (MySqlException error) when (error.Number is 1048 or 1062 or 1264 or 1366 or 1406 or 1451 or 1452 or 4025)
        {
            throw new ErrorAplicacionException(TipoError.DatosInvalidos,
                "La copia contiene datos que no cumplen las reglas de integridad de la base. No se recuperó.", "Archivo", error);
        }
        catch (MySqlException error)
        {
            throw Error.AlmacenNoDisponible(error);
        }
        catch (MigracionRechazadaException error)
        {
            throw new ErrorAplicacionException(TipoError.EstadoNoPermitido, "La base no tiene el esquema esperado. No se activa.", causa: error);
        }
        catch (EsquemaNoDisponibleException error)
        {
            throw Error.AlmacenNoDisponible(error);
        }
    }
}
