using System.Text.Json;
using System.Text.Json.Serialization;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Features.Respaldos;
using Clarisa.Infrastructure.Configuration;

namespace Clarisa.Infrastructure.Respaldos.Operaciones;

// Un archivo JSON por operación junto a las copias, para que el catálogo sobreviva a la restauración de una base anterior. Un registro
// dañado detiene el servicio de respaldos con un error explícito en vez de ocultarse.
public sealed class RegistroOperacionesArchivo(ConfiguracionRespaldos configuracion) : IRegistroOperacionesRespaldo
{
    private const int MaximoBytesArchivo = 64 * 1024;
    private const string Extension = ".json";

    private static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = true, Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    private readonly SemaphoreSlim cerrojo = new(1, 1);
    private Dictionary<Guid, OperacionRespaldo>? cache;

    public async Task<(OperacionRespaldo Operacion, bool Creada)> CrearSiNoExisteAsync(OperacionRespaldo nueva, CancellationToken ct)
    {
        await cerrojo.WaitAsync(ct);
        try
        {
            var operaciones = Cargar();
            if (operaciones.TryGetValue(nueva.Id, out var existente)) return (existente, false);
            if (!Ejecutar(() => EscrituraAtomica.Crear(Ruta(nueva.Id), Serializar(nueva))))
            {
                var leida = Leer(Ruta(nueva.Id));
                operaciones[leida.Id] = leida;
                return (leida, false);
            }
            operaciones[nueva.Id] = nueva;
            return (nueva, true);
        }
        finally
        {
            cerrojo.Release();
        }
    }

    public async Task<OperacionRespaldo?> ObtenerAsync(Guid id, CancellationToken ct)
    {
        await cerrojo.WaitAsync(ct);
        try
        {
            return Cargar().GetValueOrDefault(id);
        }
        finally
        {
            cerrojo.Release();
        }
    }

    public async Task GuardarAsync(OperacionRespaldo operacion, CancellationToken ct)
    {
        await cerrojo.WaitAsync(ct);
        try
        {
            var operaciones = Cargar();
            if (!operaciones.ContainsKey(operacion.Id)) throw Error.NoEncontrado("No existe esa operación de respaldo.");
            Ejecutar(() =>
            {
                EscrituraAtomica.Reemplazar(Ruta(operacion.Id), Serializar(operacion));
                return true;
            });
            operaciones[operacion.Id] = operacion;
        }
        finally
        {
            cerrojo.Release();
        }
    }

    public async Task<IReadOnlyList<OperacionRespaldo>> ListarAsync(CancellationToken ct)
    {
        await cerrojo.WaitAsync(ct);
        try
        {
            return Cargar().Values.ToList();
        }
        finally
        {
            cerrojo.Release();
        }
    }

    private Dictionary<Guid, OperacionRespaldo> Cargar()
    {
        if (cache is not null) return cache;
        var leidas = new Dictionary<Guid, OperacionRespaldo>();
        Ejecutar(() =>
        {
            Directory.CreateDirectory(configuracion.DirectorioOperaciones);
            foreach (var archivo in Directory.EnumerateFiles(configuracion.DirectorioOperaciones, "*" + Extension))
            {
                var operacion = Leer(archivo);
                if (Path.GetFileNameWithoutExtension(archivo) != operacion.Id.ToString("N")) throw Danado(archivo);
                leidas[operacion.Id] = operacion;
            }
            return true;
        });
        return cache = leidas;
    }

    private static OperacionRespaldo Leer(string archivo)
    {
        try
        {
            var info = new FileInfo(archivo);
            if (info.Length is 0 or > MaximoBytesArchivo) throw Danado(archivo);
            var registro = JsonSerializer.Deserialize<ArchivoOperacion>(EscrituraAtomica.Leer(archivo), Opciones);
            if (registro is not { Formato: 1, Operacion: { } operacion } || operacion.Id == Guid.Empty || operacion.Mensaje is null
                || !Enum.IsDefined(operacion.Tipo) || !Enum.IsDefined(operacion.Estado) || !Enum.IsDefined(operacion.Fase))
                throw Danado(archivo);
            return operacion;
        }
        catch (JsonException error)
        {
            throw Danado(archivo, error);
        }
    }

    private static byte[] Serializar(OperacionRespaldo operacion) => JsonSerializer.SerializeToUtf8Bytes(new ArchivoOperacion(1, operacion), Opciones);

    private string Ruta(Guid id) => Path.Combine(configuracion.DirectorioOperaciones, id.ToString("N") + Extension);

    private static T Ejecutar<T>(Func<T> accion)
    {
        try
        {
            return accion();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw Error.AlmacenNoDisponible(error);
        }
    }

    private static ErrorAplicacionException Danado(string archivo, Exception? causa = null) =>
        new(TipoError.AlmacenNoDisponible, $"El registro de operaciones de respaldo está dañado ({Path.GetFileName(archivo)}). "
            + "Revísalo con la herramienta de mantenimiento.", causa: causa);

    private sealed record ArchivoOperacion(int Formato, OperacionRespaldo Operacion);
}
