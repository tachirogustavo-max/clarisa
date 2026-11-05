using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Clarisa.Infrastructure.Seguridad;
using MySqlConnector;

namespace Clarisa.Infrastructure.Configuration;

public static class ConfiguracionEntornoServidor
{
    public const string Ajustes = "CLARISA_AJUSTES_V1";
    public const string Conexion = "CLARISA_CONEXION_V1";
    private static readonly byte[] Entropia = "Clarisa.Estudios.Entorno.v1"u8.ToArray();
    public static ArchivoConfiguracionServidor Leer(Func<string, string?> obtener)
    {
        ArgumentNullException.ThrowIfNull(obtener);
        var ajustes = Descifrar<SobreAjustes>(obtener(Ajustes));
        var conexion = Descifrar<SobreConexion>(obtener(Conexion));
        if (ajustes.Id == Guid.Empty || ajustes.Id != conexion.Id || ajustes.Configuracion is null)
            throw new ConfiguracionServidorException("Las variables del servidor no corresponden a la misma configuración.");
        // La credencial de migración solo existe en el archivo temporal de la instalación; un entorno que la traiga no es del productor.
        if (ajustes.Configuracion.Any(p => p.Key.Equals("DatosMigracion", StringComparison.OrdinalIgnoreCase)))
            throw new ConfiguracionServidorException("El entorno del servidor no admite credenciales de migración.");
        try
        {
            var cadena = new MySqlConnectionStringBuilder(conexion.Cadena);
            var seccion = ajustes.Configuracion.FirstOrDefault(p => p.Key.Equals("DatosAplicacion", StringComparison.OrdinalIgnoreCase));
            if (seccion.Value is not JsonObject datos) throw new ConfiguracionServidorException("Faltan los datos de aplicación en el entorno.");
            foreach (var nombre in datos.Select(p => p.Key).Where(k => new[] { "Servidor", "Puerto", "Base", "Usuario", "Clave" }.Contains(k, StringComparer.OrdinalIgnoreCase)).ToArray())
                datos.Remove(nombre);
            datos["Servidor"] = cadena.Server;
            datos["Puerto"] = (int)cadena.Port;
            datos["Base"] = cadena.Database;
            datos["Usuario"] = cadena.UserID;
            datos["Clave"] = cadena.Password;
            var resultado = ArchivoConfiguracionServidor.DesdeEntorno(JsonSerializer.SerializeToUtf8Bytes(ajustes.Configuracion));
            var validada = datos.Deserialize<ConfiguracionMariaDb>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new JsonException();
            validada.Validar();
            return resultado;
        }
        catch (Exception error) when (error is ArgumentException or JsonException or InvalidOperationException or OverflowException)
        { throw new ConfiguracionServidorException("La configuración de conexión del entorno no es válida."); }
    }
    private static T Descifrar<T>(string? valor)
    {
        if (string.IsNullOrEmpty(valor) || valor.Length > 24_000)
            throw new ConfiguracionServidorException("Falta una variable protegida del servidor o tiene tamaño inválido.");
        try
        {
            var contenido = ProteccionDpapi.Desproteger(Convert.FromBase64String(valor), Entropia);
            try { return JsonSerializer.Deserialize<T>(contenido) ?? throw new JsonException(); }
            finally { CryptographicOperations.ZeroMemory(contenido); }
        }
        catch (Exception error) when (error is CryptographicException or FormatException or JsonException or PlatformNotSupportedException)
        { throw new ConfiguracionServidorException("No se pudo verificar la configuración protegida del entorno."); }
    }
    private sealed record SobreAjustes(Guid Id, JsonObject Configuracion);
    private sealed record SobreConexion(Guid Id, string Cadena);
}
