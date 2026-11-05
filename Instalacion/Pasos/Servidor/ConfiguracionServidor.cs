using System.Text.Json;
using System.Text.Json.Nodes;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Registro;

namespace Clarisa.Instalacion.Pasos.Servidor;

// servidor.json lo comparten la instalación, el servidor y el mantenimiento. La instalación impone solo sus valores (escucha, certificado,
// rutas, cuenta de la aplicación) y conserva el resto; nunca escribe aquí la credencial de migración. Los nombres no distinguen
// mayúsculas, igual que en el servidor, para no crear propiedades repetidas que este rechazaría.
internal static class ConfiguracionServidor
{
    private const long MaximoBytes = 256 * 1024;

    private static readonly JsonDocumentOptions Lectura = new()
    {
        CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, MaxDepth = 32
    };

    public static string UrlEscucha(RutasServidor rutas) =>
        $"https://{RutasServidor.Anfitrion(rutas.Solicitud.DireccionEscucha.ToString())}:{rutas.Solicitud.PuertoHttps}";

    public static JsonObject Obligatoria(RutasServidor rutas, string claveAplicacion, string baseDatos) => new()
    {
        ["DatosAplicacion"] = Datos(rutas, baseDatos, RutasServidor.UsuarioAplicacion, claveAplicacion, completa: false),
        ["Seguridad"] = new JsonObject { ["PermitirHttpLoopback"] = false },
        ["Respaldos"] = new JsonObject
        {
            ["Directorio"] = rutas.Respaldos,
            ["ArchivoClave"] = rutas.ArchivoClaveRespaldos,
            ["ArchivoBaseActiva"] = rutas.ArchivoBaseActiva,
            ["ArchivoExclusion"] = rutas.ArchivoExclusion
        },
        ["Kestrel"] = new JsonObject
        {
            ["Endpoints"] = new JsonObject
            {
                ["Https"] = new JsonObject
                {
                    ["Url"] = UrlEscucha(rutas),
                    ["Certificate"] = new JsonObject { ["Path"] = rutas.ArchivoCertificado, ["KeyPath"] = rutas.ArchivoClaveCertificado }
                }
            }
        }
    };

    // Solo se escriben si faltan: el responsable técnico puede ajustarlos sin que una reparación los revierta.
    private static JsonObject Predeterminada() => new()
    {
        ["DatosAplicacion"] = Tiempos(new JsonObject()),
        ["Seguridad"] = new JsonObject { ["ArchivoClavesProhibidas"] = "Resources/claves-prohibidas.txt" }
    };

    public static JsonObject Datos(RutasServidor rutas, string baseDatos, string usuario, string clave, bool completa = true)
    {
        var datos = new JsonObject
        {
            ["Servidor"] = "127.0.0.1", ["Puerto"] = rutas.Solicitud.PuertoMotor, ["Base"] = baseDatos, ["Usuario"] = usuario, ["Clave"] = clave
        };
        return completa ? Tiempos(datos) : datos;
    }

    private static JsonObject Tiempos(JsonObject datos)
    {
        datos["SegundosConexion"] = 5;
        datos["SegundosComando"] = 30;
        datos["SegundosEsperaBloqueo"] = 10;
        datos["MaximoConexiones"] = 20;
        return datos;
    }

    public static JsonObject Fusionar(JsonObject? actual, JsonObject obligatoria)
    {
        var resultado = actual?.DeepClone() as JsonObject ?? new JsonObject();
        Quitar(resultado, "DatosMigracion");
        Quitar(resultado, "Urls");
        if (Hijo(Hijo(resultado, "Kestrel"), "Endpoints") is JsonObject puntos)
            foreach (var otro in puntos.Select(p => p.Key).Where(n => !n.Equals("Https", StringComparison.OrdinalIgnoreCase)).ToList())
                puntos.Remove(otro);
        Copiar(resultado, obligatoria, sobrescribir: true);
        Copiar(resultado, Predeterminada(), sobrescribir: false);
        return resultado;
    }

    public static string? Diferencia(JsonObject actual, JsonObject obligatoria)
    {
        if (Hijo(actual, "DatosMigracion") is not null) return "La configuración permanente contiene credenciales de migración.";
        if (Hijo(actual, "Urls") is not null || Hijo(Hijo(actual, "Kestrel"), "Endpoints") is JsonObject { Count: not 1 })
            return "La configuración abre puntos de escucha distintos del elegido.";
        if (!Contiene(actual, obligatoria, compararValores: true)) return "La configuración difiere de la elegida en esta instalación.";
        return Contiene(actual, Predeterminada(), compararValores: false) ? null : "A la configuración le faltan valores necesarios.";
    }

    public static JsonObject? Leer(string ruta)
    {
        RutasSeguras.ExigirSinPuntosReparacion(ruta);
        var info = new FileInfo(ruta);
        if (!info.Exists) return null;
        if (info.Length > MaximoBytes) throw new FalloInstalacionException("La configuración del servidor es demasiado grande.");
        try
        {
            var nodo = JsonNode.Parse(File.ReadAllBytes(info.FullName), documentOptions: Lectura) as JsonObject
                       ?? throw new FalloInstalacionException("La configuración del servidor no es un objeto JSON.");
            ExigirNombresUnicos(nodo);
            return nodo;
        }
        catch (Exception error) when (error is JsonException or ArgumentException)
        {
            throw new FalloInstalacionException($"No se puede interpretar {ruta}; no se sobrescribe a ciegas. Revísalo o restáuralo.", error);
        }
    }

    public static string? UrlConfigurada(RutasServidor rutas)
    {
        try
        {
            return Texto(Hijo(Hijo(Hijo(Hijo(Leer(rutas.ArchivoConfiguracion), "Kestrel"), "Endpoints"), "Https"), "Url"));
        }
        catch (FalloInstalacionException)
        {
            return null;
        }
    }

    public static string? UrlConfigurada(JsonObject? configuracion) =>
        Texto(Hijo(Hijo(Hijo(Hijo(configuracion, "Kestrel"), "Endpoints"), "Https"), "Url"));

    public static bool MismoContenido(JsonNode? uno, JsonNode? otro)
    {
        if (uno is JsonObject a && otro is JsonObject b)
            return a.Count == b.Count && a.All(p => Nombre(b, p.Key) is { } nombre && MismoContenido(p.Value, b[nombre]));
        if (uno is JsonArray lista && otro is JsonArray otra)
            return lista.Count == otra.Count && lista.Select((n, i) => MismoContenido(n, otra[i])).All(igual => igual);
        return JsonNode.DeepEquals(uno, otro);
    }

    public static int? PuertoConfigurado(JsonObject? configuracion) =>
        Hijo(Hijo(configuracion, "DatosAplicacion"), "Puerto") is JsonValue valor && valor.TryGetValue<int>(out var puerto) ? puerto : null;

    public static string? ClaveAplicacion(JsonObject? configuracion) =>
        Texto(Hijo(Hijo(configuracion, "DatosAplicacion"), "Clave")) is { Length: >= 16 } clave ? clave : null;

    // La base puede haber cambiado por decisión del responsable; la referencia de recuperación la gestiona el mantenimiento.
    public static string BaseConfigurada(JsonObject? configuracion) =>
        Texto(Hijo(Hijo(configuracion, "DatosAplicacion"), "Base")) is { Length: > 0 } nombre ? nombre : RutasServidor.BaseActiva;

    public static void Escribir(string ruta, JsonObject contenido) =>
        EscrituraSegura.Reemplazar(ruta, flujo => JsonSerializer.Serialize(flujo, contenido, new JsonSerializerOptions { WriteIndented = true }));

    // Sobrescribe antes de borrar para no dejar la credencial temporal en claro en el mismo bloque del archivo.
    public static void BorrarTemporal(string ruta)
    {
        var info = new FileInfo(ruta);
        if (!info.Exists) return;
        using (var flujo = new FileStream(ruta, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            flujo.Write(new byte[info.Length]);
            flujo.Flush(flushToDisk: true);
        }
        File.Delete(ruta);
    }

    private static JsonNode? Hijo(JsonNode? padre, string nombre) =>
        padre is JsonObject objeto && Nombre(objeto, nombre) is { } existente ? objeto[existente] : null;

    private static string? Nombre(JsonObject objeto, string nombre) =>
        objeto.Select(p => p.Key).FirstOrDefault(k => k.Equals(nombre, StringComparison.OrdinalIgnoreCase));

    private static string? Texto(JsonNode? nodo) => nodo is JsonValue valor && valor.TryGetValue<string>(out var texto) ? texto : null;

    private static void Quitar(JsonObject objeto, string nombre)
    {
        if (Nombre(objeto, nombre) is { } existente) objeto.Remove(existente);
    }

    private static void Copiar(JsonObject destino, JsonObject plantilla, bool sobrescribir)
    {
        foreach (var (clave, valor) in plantilla)
        {
            var existente = Nombre(destino, clave);
            if (valor is JsonObject hijo)
            {
                if (existente is null || destino[existente] is not JsonObject)
                {
                    if (existente is not null && !sobrescribir) continue;
                    destino[existente ?? clave] = new JsonObject();
                }
                Copiar((JsonObject)destino[existente ?? clave]!, hijo, sobrescribir);
            }
            else if (existente is null || sobrescribir)
            {
                destino[existente ?? clave] = valor?.DeepClone();
            }
        }
    }

    private static bool Contiene(JsonObject actual, JsonObject plantilla, bool compararValores) => plantilla.All(p =>
    {
        var nodo = Hijo(actual, p.Key);
        return p.Value is JsonObject hijo
            ? nodo is JsonObject objeto && Contiene(objeto, hijo, compararValores)
            : nodo is not null && (!compararValores || JsonNode.DeepEquals(nodo, p.Value));
    });

    internal static void ExigirNombresUnicos(JsonNode? nodo)
    {
        switch (nodo)
        {
            case JsonObject objeto:
                if (objeto.Select(p => p.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != objeto.Count)
                    throw new FalloInstalacionException("La configuración del servidor repite una propiedad en la misma sección.");
                foreach (var (_, hijo) in objeto) ExigirNombresUnicos(hijo);
                break;
            case JsonArray lista:
                foreach (var hijo in lista) ExigirNombresUnicos(hijo);
                break;
        }
    }
}
