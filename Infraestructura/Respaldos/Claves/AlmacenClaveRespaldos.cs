using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Features.Respaldos;
using Clarisa.Infrastructure.Configuration;
using Clarisa.Infrastructure.Seguridad;

namespace Clarisa.Infrastructure.Respaldos.Claves;

// Copia de la clave en el servidor, protegida con DPAPI de equipo y entropía ligada a su Id. El archivo no contiene la clave en claro.
public sealed class AlmacenClaveRespaldos(ConfiguracionRespaldos configuracion) : IClaveRespaldos
{
    private const int MaximoBytesArchivo = 16 * 1024;

    public Task<bool> EstaPreparadaAsync(CancellationToken ct) => Task.FromResult(File.Exists(configuracion.ArchivoClave));

    internal ClaveRespaldo Cargar()
    {
        var info = new FileInfo(configuracion.ArchivoClave);
        if (!info.Exists) throw ErroresRespaldo.SinClave();
        try
        {
            if (info.Length > MaximoBytesArchivo) throw new InvalidDataException("Archivo de clave demasiado grande.");
            var archivo = JsonSerializer.Deserialize<ArchivoClave>(File.ReadAllBytes(info.FullName))
                ?? throw new InvalidDataException("Archivo de clave vacío.");
            if (archivo.Formato != 1 || archivo.Id == Guid.Empty) throw new InvalidDataException("Formato de clave no admitido.");
            var material = ProteccionDpapi.Desproteger(Convert.FromBase64String(archivo.Protegida), Entropia(archivo.Id));
            try
            {
                return new ClaveRespaldo(archivo.Id, material);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(material);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or FormatException
                                          or InvalidDataException or CryptographicException or PlatformNotSupportedException)
        {
            throw ErroresRespaldo.ClaveLocalIlegible(error);
        }
    }

    internal ClaveRespaldo Obtener(OrigenClave origen) =>
        origen.ArchivoRecuperacion is { Length: > 0 } ruta ? CodigoRecuperacion.LeerArchivo(ruta) : Cargar();

    // El recurso de recuperación se escribe antes que la copia protegida: sin exportación no queda una clave sin respaldo.
    public Guid CrearYExportar(string destinoRecuperacion)
    {
        ExigirDestinoNuevo(destinoRecuperacion);
        if (File.Exists(configuracion.ArchivoClave))
            throw Error.EstadoNoPermitido("El servidor ya tiene una clave de respaldos. No se sustituye: las copias existentes dependen de ella.");
        using var clave = ClaveRespaldo.Generar();
        if (!EscrituraAtomica.Crear(destinoRecuperacion, Encoding.UTF8.GetBytes(CodigoRecuperacion.Componer(clave))))
            throw Error.Duplicado("Ya existe un archivo con ese nombre de recuperación.", "Destino");
        try
        {
            Guardar(clave);
        }
        catch
        {
            File.Delete(destinoRecuperacion);
            throw;
        }
        return clave.Id;
    }

    public Guid Exportar(string destinoRecuperacion)
    {
        ExigirDestinoNuevo(destinoRecuperacion);
        using var clave = Cargar();
        if (!EscrituraAtomica.Crear(destinoRecuperacion, Encoding.UTF8.GetBytes(CodigoRecuperacion.Componer(clave))))
            throw Error.Duplicado("Ya existe un archivo con ese nombre de recuperación.", "Destino");
        return clave.Id;
    }

    // Instala en un servidor nuevo la clave de un archivo de recuperación. Nunca sustituye una clave distinta ya preparada.
    public Guid Importar(string archivoRecuperacion)
    {
        using var clave = CodigoRecuperacion.LeerArchivo(archivoRecuperacion);
        if (File.Exists(configuracion.ArchivoClave))
        {
            using var actual = Cargar();
            return actual.Id == clave.Id && CryptographicOperations.FixedTimeEquals(actual.Material, clave.Material)
                ? actual.Id
                : throw Error.EstadoNoPermitido("El servidor ya tiene otra clave de respaldos. No se sustituye.");
        }
        Guardar(clave);
        return clave.Id;
    }

    public Guid? IdPreparada()
    {
        if (!File.Exists(configuracion.ArchivoClave)) return null;
        using var clave = Cargar();
        return clave.Id;
    }

    private void Guardar(ClaveRespaldo clave)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(configuracion.ArchivoClave)!);
        var protegida = ProteccionDpapi.Proteger(clave.Material, Entropia(clave.Id));
        var contenido = JsonSerializer.SerializeToUtf8Bytes(new ArchivoClave(1, clave.Id, Convert.ToBase64String(protegida)));
        if (!EscrituraAtomica.Crear(configuracion.ArchivoClave, contenido))
            throw Error.EstadoNoPermitido("El servidor ya tiene una clave de respaldos. No se sustituye.");
    }

    private static void ExigirDestinoNuevo(string destino)
    {
        if (string.IsNullOrWhiteSpace(destino) || !Path.IsPathFullyQualified(destino))
            throw Error.DatosInvalidos("Indica una ruta absoluta para el archivo de recuperación.", "Destino");
        if (File.Exists(destino)) throw Error.Duplicado("Ya existe un archivo con ese nombre de recuperación.", "Destino");
    }

    // La entropía forma parte de la protección de las claves ya guardadas: cambiarla las haría ilegibles.
    private static byte[] Entropia(Guid id) => Encoding.ASCII.GetBytes("Escolar.Respaldos.Clave.v1:" + id.ToString("D"));

    private sealed record ArchivoClave(int Formato, Guid Id, string Protegida);
}
