using System.Security.Cryptography;
using System.Text;
using Clarisa.Instalacion.Ejecucion;

namespace Clarisa.Instalacion.Pasos.Servidor;

// Clave de administración del motor: aleatoria, protegida con DPAPI de equipo y en una carpeta solo de Administradores y SYSTEM.
internal static class CredencialMotor
{
    private const int MaximoBytes = 4096;

    public static string Generar() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(30)).Replace('+', '-').Replace('/', '_');

    public static string? Leer(ContextoEjecucion contexto)
    {
        var info = new FileInfo(contexto.RutasServidor.ArchivoAdministracionMotor);
        if (!info.Exists) return null;
        if (info.Length > MaximoBytes) throw new FalloInstalacionException("La credencial protegida del motor está dañada.");
        try
        {
            var clave = Encoding.UTF8.GetString(contexto.Sistema.Proteccion.Desproteger(File.ReadAllBytes(info.FullName)));
            contexto.Filtro.Registrar(clave);
            return clave;
        }
        catch (CryptographicException error)
        {
            throw new FalloInstalacionException("La credencial protegida del motor no se puede leer en este equipo.", error);
        }
    }

    public static string Exigir(ContextoEjecucion contexto) => Leer(contexto)
        ?? throw new FalloInstalacionException("Falta la credencial de administración del motor; el motor no se inicializó con este instalador.");

    public static void Guardar(ContextoEjecucion contexto, string clave)
    {
        var ruta = contexto.RutasServidor.ArchivoAdministracionMotor;
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        using var flujo = new FileStream(ruta, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        flujo.Write(contexto.Sistema.Proteccion.Proteger(Encoding.UTF8.GetBytes(clave)));
        flujo.Flush(flushToDisk: true);
    }
}
