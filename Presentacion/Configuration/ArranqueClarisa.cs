using Clarisa.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration.Memory;

namespace Clarisa.Api.Configuration;

public static class ArranqueClarisa
{
    public static WebApplicationBuilder CrearConstructor(string[] argumentos)
    {
        var constructor = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = argumentos,
            ApplicationName = typeof(ArranqueClarisa).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory
        });
        constructor.Services.AddWindowsService(opciones => opciones.ServiceName = "Clarisa.Estudios");
        return constructor;
    }

    // Solo se incorpora la fuente validada elegida; no se mezclan valores ajenos de appsettings, argumentos o variables globales.
    public static WebApplicationBuilder CrearConstructor(ArchivoConfiguracionServidor archivo)
    {
        var constructor = CrearConstructor([]);
        foreach (var fuente in constructor.Configuration.Sources.Where(f => f is not MemoryConfigurationSource).ToList())
            constructor.Configuration.Sources.Remove(fuente);
        constructor.Configuration.AddJsonStream(new MemoryStream(archivo.Contenido, writable: false));
        return constructor;
    }

    // La base efectiva sale de la referencia activa; la migración trabaja sobre esa misma base.
    public static void FijarBaseActiva(ConfigurationManager configuracion, string baseActiva, bool migrar)
    {
        var valores = new Dictionary<string, string?> { ["DatosAplicacion:Base"] = baseActiva };
        if (migrar) valores["DatosMigracion:Base"] = baseActiva;
        configuracion.AddInMemoryCollection(valores);
    }
}
