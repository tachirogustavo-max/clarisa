namespace Clarisa.Mantenimiento;

// Solo rutas e identificadores. Cualquier opción no prevista se rechaza para que nadie pase una clave por la línea de órdenes.
internal sealed class Opciones
{
    private static readonly HashSet<string> Permitidas =
        ["configuracion", "archivo", "clave-recuperacion", "exportar", "destino", "origen", "recuperacion", "operacion"];

    private readonly Dictionary<string, string> valores = new(StringComparer.Ordinal);

    public static Opciones Interpretar(IReadOnlyList<string> argumentos)
    {
        var opciones = new Opciones();
        for (var indice = 0; indice < argumentos.Count; indice++)
        {
            var nombre = argumentos[indice].StartsWith("--", StringComparison.Ordinal) ? argumentos[indice][2..] : "";
            if (nombre is "entorno" or "entorno-servicio")
            {
                if (opciones.valores.ContainsKey("configuracion") || opciones.valores.ContainsKey("entorno") || opciones.valores.ContainsKey("entorno-servicio"))
                    throw new ArgumentException("Indica una sola fuente de configuración.");
                opciones.valores.Add(nombre, "1");
                continue;
            }
            if (nombre == "configuracion" && (opciones.valores.ContainsKey("entorno") || opciones.valores.ContainsKey("entorno-servicio")))
                throw new ArgumentException("Indica una sola fuente de configuración.");
            if (!Permitidas.Contains(nombre)) throw new ArgumentException($"Opción no admitida: {argumentos[indice]}.");
            if (indice + 1 >= argumentos.Count || argumentos[indice + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Falta el valor de --{nombre}.");
            if (!opciones.valores.TryAdd(nombre, argumentos[indice + 1])) throw new ArgumentException($"La opción --{nombre} se repite.");
            indice++;
        }
        return opciones;
    }

    public string? Valor(string nombre) => valores.GetValueOrDefault(nombre);

    public string Ruta(string nombre) => RutaOpcional(nombre) ?? throw new ArgumentException($"Indica --{nombre}.");

    public string? RutaOpcional(string nombre) => Valor(nombre) is { Length: > 0 } ruta ? Path.GetFullPath(ruta) : null;
}
