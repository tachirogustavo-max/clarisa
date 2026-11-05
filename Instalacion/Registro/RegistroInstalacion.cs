using System.Text;
using System.Text.Json;

namespace Clarisa.Instalacion.Registro;

internal sealed record EntradaRegistro(Guid Ejecucion, DateTimeOffset Fecha, string Etapa, string Resultado, string Detalle);

// Una línea JSON por evento, escrita y volcada al disco antes de continuar: tras un corte eléctrico queda lo último que se hizo.
internal sealed class RegistroInstalacion : IDisposable
{
    private readonly FileStream archivo;
    private readonly FiltroSecretos filtro;
    private readonly Func<DateTimeOffset> reloj;
    private readonly Guid ejecucion;

    private RegistroInstalacion(FileStream archivo, Guid ejecucion, FiltroSecretos filtro, Func<DateTimeOffset> reloj)
    {
        this.archivo = archivo;
        this.ejecucion = ejecucion;
        this.filtro = filtro;
        this.reloj = reloj;
    }

    public string Ruta => archivo.Name;

    public static RegistroInstalacion Abrir(string directorio, Guid ejecucion, FiltroSecretos filtro, Func<DateTimeOffset> reloj,
        string prefijo = "instalacion")
    {
        RutasSeguras.ExigirSinPuntosReparacion(directorio);
        Directory.CreateDirectory(directorio);
        var nombre = $"{prefijo}-{reloj():yyyyMMdd-HHmmss}-{ejecucion:N}.jsonl";
        var flujo = new FileStream(Path.Combine(directorio, nombre), FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096,
            FileOptions.WriteThrough);
        return new RegistroInstalacion(flujo, ejecucion, filtro, reloj);
    }

    public void Escribir(string etapa, string resultado, string detalle)
    {
        var entrada = new EntradaRegistro(ejecucion, reloj(), etapa, resultado, filtro.Limpiar(detalle));
        var linea = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entrada) + "\n");
        archivo.Write(linea);
        archivo.Flush(flushToDisk: true);
    }

    public void Dispose() => archivo.Dispose();
}
