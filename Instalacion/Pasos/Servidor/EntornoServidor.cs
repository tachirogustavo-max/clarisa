using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Clarisa.Instalacion.Ejecucion;
using MySqlConnector;

namespace Clarisa.Instalacion.Pasos.Servidor;

internal static class EntornoServidor
{
    public const string Ajustes = "CLARISA_AJUSTES_V1";
    public const string Conexion = "CLARISA_CONEXION_V1";
    private static readonly byte[] Entropia = "Clarisa.Estudios.Entorno.v1"u8.ToArray();
    public static IReadOnlyDictionary<string, string> Crear(JsonObject configuracion)
    {
        var copia = (JsonObject)configuracion.DeepClone();
        var datos = copia.FirstOrDefault(p => p.Key.Equals("DatosAplicacion", StringComparison.OrdinalIgnoreCase)).Value as JsonObject
            ?? throw new FalloInstalacionException("Faltan los datos de aplicación para preparar el entorno.");
        JsonNode? Valor(string clave) => datos.FirstOrDefault(p => p.Key.Equals(clave, StringComparison.OrdinalIgnoreCase)).Value;
        string Texto(string clave) => Valor(clave) is JsonValue valor && valor.TryGetValue<string>(out var texto) ? texto
            : throw new FalloInstalacionException("Faltan valores de conexión para preparar el entorno o no son texto.");
        var puerto = Valor("Puerto") switch
        {
            null => 3306,
            JsonValue valor when valor.TryGetValue<int>(out var numero) && numero is > 0 and <= 65535 => numero,
            _ => throw new FalloInstalacionException("El puerto de la conexión no es un número válido.")
        };
        var cadena = new MySqlConnectionStringBuilder
        { Server = Texto("Servidor"), Port = (uint)puerto, Database = Texto("Base"), UserID = Texto("Usuario"), Password = Texto("Clave") }.ConnectionString;
        foreach (var clave in datos.Select(p => p.Key).Where(k => k.Equals("Clave", StringComparison.OrdinalIgnoreCase)).ToArray()) datos.Remove(clave);
        foreach (var clave in copia.Select(p => p.Key).Where(k => k.Equals("DatosMigracion", StringComparison.OrdinalIgnoreCase)).ToArray()) copia.Remove(clave);
        var id = Guid.NewGuid();
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Ajustes] = Proteger(JsonSerializer.SerializeToUtf8Bytes(new SobreAjustes(id, copia))),
            [Conexion] = Proteger(JsonSerializer.SerializeToUtf8Bytes(new SobreConexion(id, cadena)))
        };
    }
    public static JsonObject Leer(IReadOnlyDictionary<string, string> valores)
    {
        if (valores.Count != 2 || !valores.ContainsKey(Ajustes) || !valores.ContainsKey(Conexion))
            throw new FalloInstalacionException("El entorno del servidor está incompleto o tiene variables no admitidas.");
        var ajustes = Desproteger<SobreAjustes>(valores[Ajustes]);
        var conexion = Desproteger<SobreConexion>(valores[Conexion]);
        if (ajustes.Id == Guid.Empty || ajustes.Id != conexion.Id || ajustes.Configuracion is null)
            throw new FalloInstalacionException("Las variables del servidor no corresponden a la misma configuración.");
        if (ajustes.Configuracion.Any(p => p.Key.Equals("DatosMigracion", StringComparison.OrdinalIgnoreCase)))
            throw new FalloInstalacionException("El entorno del servidor no admite credenciales de migración.");
        ConfiguracionServidor.ExigirNombresUnicos(ajustes.Configuracion);
        MySqlConnectionStringBuilder cadena;
        try { cadena = new(conexion.Cadena); }
        catch (ArgumentException) { throw new FalloInstalacionException("La conexión protegida no es válida."); }
        var datos = ajustes.Configuracion.FirstOrDefault(p => p.Key.Equals("DatosAplicacion", StringComparison.OrdinalIgnoreCase)).Value as JsonObject
            ?? throw new FalloInstalacionException("Faltan los datos del servidor en el entorno.");
        foreach (var nombre in datos.Select(p => p.Key).Where(k => new[] { "Servidor", "Puerto", "Base", "Usuario", "Clave" }.Contains(k, StringComparer.OrdinalIgnoreCase)).ToArray())
            datos.Remove(nombre);
        datos["Servidor"] = cadena.Server;
        datos["Puerto"] = (int)cadena.Port;
        datos["Base"] = cadena.Database;
        datos["Usuario"] = cadena.UserID;
        datos["Clave"] = cadena.Password;
        return ajustes.Configuracion;
    }
    private static string Proteger(byte[] contenido)
    {
        try
        {
            if (contenido.Length > 16_000) throw new FalloInstalacionException("La configuración supera el tamaño admitido para el entorno del servicio.");
            return Convert.ToBase64String(ProtectedData.Protect(contenido, Entropia, DataProtectionScope.LocalMachine));
        }
        finally { CryptographicOperations.ZeroMemory(contenido); }
    }
    private static T Desproteger<T>(string valor)
    {
        if (string.IsNullOrEmpty(valor) || valor.Length > 24_000) throw new FalloInstalacionException("Una variable protegida tiene tamaño inválido.");
        try
        {
            var contenido = ProtectedData.Unprotect(Convert.FromBase64String(valor), Entropia, DataProtectionScope.LocalMachine);
            try { return JsonSerializer.Deserialize<T>(contenido) ?? throw new JsonException(); }
            finally { CryptographicOperations.ZeroMemory(contenido); }
        }
        catch (Exception error) when (error is CryptographicException or FormatException or JsonException)
        { throw new FalloInstalacionException("No se pudo verificar la configuración protegida del servicio."); }
    }
    private sealed record SobreAjustes(Guid Id, JsonObject Configuracion);
    private sealed record SobreConexion(Guid Id, string Cadena);
}
