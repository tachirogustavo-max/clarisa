using System.Text.Json;
using System.Text.Json.Serialization;
using Clarisa.Infrastructure.Configuration;
using Clarisa.Infrastructure.Seguridad;

namespace Clarisa.Mantenimiento;

// Lee el mismo archivo protegido que el servidor, validado por el mismo lector; solo las secciones DatosAplicacion y Respaldos. Los
// nombres de sección no distinguen mayúsculas, igual que la configuración del servidor.
internal sealed record ConfiguracionMantenimiento(ConfiguracionMariaDb Datos, ConfiguracionRespaldos Respaldos)
{
    public PoliticaClaveLocal Seguridad { get; init; } = new();
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true, NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static ConfiguracionMantenimiento Cargar(string? ruta, bool desdeEntorno = false, bool desdeServicio = false)
    {
        if ((ruta is not null ? 1 : 0) + (desdeEntorno ? 1 : 0) + (desdeServicio ? 1 : 0) != 1)
            throw new ConfiguracionServidorException("Indica una sola fuente de configuración para mantenimiento.");
        var archivo = desdeEntorno ? ConfiguracionEntornoServidor.Leer(Environment.GetEnvironmentVariable)
            : desdeServicio ? EntornoServicioRegistrado.Leer() : ArchivoConfiguracionServidor.Leer(ruta, AppContext.BaseDirectory);
        using var documento = archivo.Documento();
        var datos = Seccion<ConfiguracionMariaDb>(documento, "DatosAplicacion");
        var respaldos = Seccion<ConfiguracionRespaldos>(documento, "Respaldos");
        datos.Validar();
        var seguridad = documento.RootElement.EnumerateObject().FirstOrDefault(p => string.Equals(p.Name, "Seguridad", StringComparison.OrdinalIgnoreCase));
        PoliticaClaveLocal politica;
        try { politica = seguridad.Value.ValueKind == JsonValueKind.Undefined ? new() : seguridad.Value.Deserialize<PoliticaClaveLocal>(Opciones) ?? new(); }
        catch (JsonException) { throw new ConfiguracionServidorException("La política de contraseñas no es válida."); }
        if (politica.IteracionesClave is < ProtectorClavesPbkdf2.IteracionesMinimas or > ProtectorClavesPbkdf2.IteracionesMaximas)
            throw new ConfiguracionServidorException("El costo de contraseñas configurado no es válido.");
        return new(datos, respaldos.Validar()) { Seguridad = politica };
    }

    private static T Seccion<T>(JsonDocument documento, string nombre)
    {
        var seccion = documento.RootElement.EnumerateObject().FirstOrDefault(p => string.Equals(p.Name, nombre, StringComparison.OrdinalIgnoreCase));
        if (seccion.Value.ValueKind != JsonValueKind.Object)
            throw new ConfiguracionServidorException($"Falta la sección {nombre} en la configuración del servidor.");
        try
        {
            return seccion.Value.Deserialize<T>(Opciones) ?? throw new ConfiguracionServidorException($"La sección {nombre} está vacía.");
        }
        catch (JsonException)
        {
            throw new ConfiguracionServidorException($"La sección {nombre} no es válida.");
        }
    }
}

internal sealed record PoliticaClaveLocal
{
    public int IteracionesClave { get; init; } = ProtectorClavesPbkdf2.IteracionesPredeterminadas;
    public string? ArchivoClavesProhibidas { get; init; }
}
