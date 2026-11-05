using System.Security.Cryptography;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Features.Respaldos;
using Clarisa.Infrastructure.Configuration;
using Clarisa.Infrastructure.Respaldos.Claves;
using Clarisa.Infrastructure.Respaldos.Formato;
using Clarisa.Infrastructure.Respaldos.Operaciones;
using MySqlConnector;

namespace Clarisa.Infrastructure.Respaldos.MariaDb;

public sealed class GeneradorCopiasMariaDb(ConfiguracionMariaDb datos, ConfiguracionRespaldos configuracion, AlmacenClaveRespaldos claves,
    AlmacenCopiasArchivo almacen) : IGeneradorCopias
{
    internal LimitesPaquete Limites => LimitesDe(configuracion);

    public async Task<CopiaPreparada> PrepararAsync(Guid operacionId, DateTimeOffset creadaUtc, CancellationToken ct)
    {
        using var clave = claves.Cargar();
        var plano = await EmpaquetarAsync(creadaUtc, ct);
        try
        {
            await using var archivo = almacen.CrearTemporal(operacionId);
            SobreCifrado.Escribir(archivo, clave, plano);
            await archivo.FlushAsync(ct);
            archivo.Flush(flushToDisk: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new ErrorAplicacionException(TipoError.AlmacenNoDisponible,
                "No se pudo escribir la copia en el disco del servidor (espacio o permisos). No se publicó ningún archivo.", causa: error);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plano);
        }
        return await VerificarArchivoAsync(almacen.Temporal(operacionId), clave, Limites, ct);
    }

    // Relectura completa desde disco: descifra, autentica y recorre todas las tablas comparándolas con el manifiesto.
    internal static async Task<CopiaPreparada> VerificarArchivoAsync(string ruta, ClaveRespaldo clave, LimitesPaquete limites,
        CancellationToken ct)
    {
        await using var archivo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);
        var huella = Convert.ToHexString(await SHA256.HashDataAsync(archivo, ct));
        archivo.Position = 0;
        var plano = SobreCifrado.Leer(archivo, clave, limites.MaximoCifrado);
        try
        {
            using var paquete = LectorPaquete.Abrir(plano, limites);
            await paquete.ValidarTodoAsync(ct);
            return new(archivo.Length, huella);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plano);
        }
    }

    internal static LimitesPaquete LimitesDe(ConfiguracionRespaldos configuracion) =>
        new(configuracion.MaximoBytesCifrado, configuracion.MaximoBytesDescomprimidos, configuracion.MaximoFilasPorTabla);

    // Una sola transacción REPEATABLE READ con snapshot explícito: versiones de esquema y todas las tablas ven el mismo instante.
    private async Task<byte[]> EmpaquetarAsync(DateTimeOffset creadaUtc, CancellationToken ct)
    {
        try
        {
            await using var conexion = await ConexionRespaldo.AbrirAsync(datos, sinBase: false, ct);
            await ConexionRespaldo.EjecutarAsync(conexion, "SET SESSION TRANSACTION ISOLATION LEVEL REPEATABLE READ", ct);
            await ConexionRespaldo.EjecutarAsync(conexion, "START TRANSACTION WITH CONSISTENT SNAPSHOT, READ ONLY", ct);
            var migraciones = await LecturaTablasMariaDb.ExigirEsquemaAsync(conexion, ct);
            using var paquete = new EscritorPaquete(Limites);
            var tablas = new List<TablaManifiesto>();
            foreach (var tabla in CatalogoRespaldo.Tablas)
            {
                using var escritura = paquete.Iniciar(tabla);
                await LecturaTablasMariaDb.RecorrerAsync(conexion, tabla, linea => escritura.Agregar(linea.Span), ct);
                tablas.Add(escritura.Terminar());
            }
            await ConexionRespaldo.EjecutarAsync(conexion, "COMMIT", ct);
            return paquete.Finalizar(new Manifiesto(CatalogoRespaldo.Formato, Manifiesto.NombreAplicacion, creadaUtc.ToUniversalTime(),
                migraciones, tablas));
        }
        catch (MySqlException error) when (!ct.IsCancellationRequested)
        {
            throw Error.AlmacenNoDisponible(error);
        }
    }
}
