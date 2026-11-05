using System.ComponentModel;
using System.Security.Cryptography;
using Clarisa.Instalacion.Contratos;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Retirada;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Pasos.Comunes;

// Último paso: copia la herramienta en curso a una carpeta protegida propia, escribe la referencia con el inventario de programas y
// registra la entrada de Aplicaciones instaladas. Nunca apunta al Setup descargado. Un fallo deja el paso sin comprobar (instalación
// Incompleta) y no detiene la API.
internal sealed class PasoRegistroRetirada : IPasoInstalacion
{
    private const string Pendiente = ":pendiente";

    public static readonly IReadOnlyList<ReglaAcceso> Acceso =
        [new(IdentidadServicio.Administradores, DerechoAcceso.Control), new(IdentidadServicio.Sistema, DerechoAcceso.Control)];

    public string Id => "registro-retirada";
    public string Nombre => "Retirada registrada en Windows";
    public string Descripcion => "Guarda una copia protegida del instalador y su inventario, y añade la entrada en Aplicaciones instaladas.";
    public bool Critico => true;
    public bool Privilegiado => true;

    public async Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        if (contexto.Hechos.TryGetValue(Id + Pendiente, out var pendiente)) return Comprobacion.Indeterminada(pendiente);
        var diferencia = await DiferenciaAsync(contexto, ct);
        if (diferencia is null && contexto.Hechos.TryGetValue(Id, out var hecho)) return Comprobacion.Satisfecha(hecho);
        return diferencia is null
            ? Comprobacion.Satisfecha("Herramienta protegida, inventario y entrada de Aplicaciones instaladas verificados.")
            : Comprobacion.NoSatisfecha(diferencia);
    }

    public async Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        try
        {
            contexto.Hechos[Id] = await RegistrarAsync(contexto, ct);
        }
        catch (Exception error) when (error is FalloInstalacionException or IOException or UnauthorizedAccessException or Win32Exception
                                          or CryptographicException or InvalidDataException)
        {
            contexto.Hechos[Id + Pendiente] = "No se registró la retirada en Windows: " + contexto.Filtro.Limpiar(error.Message) +
                                              " El resto de la instalación no cambia; vuelve a ejecutar el instalador para completarla.";
        }
    }

    private static async Task<string> RegistrarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var herramienta = contexto.Sistema.Herramienta;
        if (herramienta is null || !Path.IsPathFullyQualified(herramienta) || !File.Exists(herramienta)
            || !string.Equals(Path.GetFileName(herramienta), ReferenciaRetirada.NombreHerramienta, StringComparison.OrdinalIgnoreCase))
            throw new FalloInstalacionException("la retirada solo se registra desde la distribución empaquetada de archivo único (Clarisa.Setup.exe).");
        var carpeta = ReferenciaRetirada.Carpeta(contexto.Plan.DirectorioRegistros);
        RutasSeguras.ExigirSinPuntosReparacion(carpeta);
        if (Directory.Exists(carpeta) && !contexto.Estado.EsPropio(TipoArtefacto.Herramienta, carpeta))
            throw new FalloInstalacionException($"la carpeta {carpeta} ya existía y no consta como propia; no se adopta.");
        var clave = RegistroRetirada.Clave(contexto.Plan.Modo);
        if (contexto.Sistema.Aplicaciones.Leer(clave) is not null && !contexto.Estado.EsPropio(TipoArtefacto.RegistroAplicacion, clave))
            throw new FalloInstalacionException($"ya existe la entrada {clave} de Aplicaciones instaladas y no la creó esta instalación; no se modifica.");
        contexto.Sistema.Permisos.Restringir(carpeta, Acceso);
        if (contexto.Sistema.Permisos.Leer(carpeta)?.Coincide(Acceso) != true)
            throw new FalloInstalacionException("no se pudo comprobar la protección de la carpeta de retirada.");
        contexto.Propio(TipoArtefacto.Herramienta, carpeta, conservar: true);

        var copia = Path.Combine(carpeta, ReferenciaRetirada.NombreHerramienta);
        var (bytes, huella) = await HuellaAsync(herramienta, ct) ?? throw new FalloInstalacionException("no se pudo leer la herramienta en curso.");
        if (!File.Exists(copia) || await HuellaAsync(copia, ct) != (bytes, huella))
            EscrituraSegura.Reemplazar(copia, destino =>
            {
                using var origen = new FileStream(herramienta, FileMode.Open, FileAccess.Read, FileShare.Read);
                origen.CopyTo(destino);
            });
        if (await HuellaAsync(copia, ct) != (bytes, huella)) throw new FalloInstalacionException("la copia de la herramienta no coincide con el original.");

        var archivo = ReferenciaRetirada.Archivo(carpeta);
        var anterior = File.Exists(archivo) ? ReferenciaRetirada.Leer(archivo) : null;
        var referencia = new ReferenciaRetirada(1, anterior?.Instalacion ?? Guid.NewGuid(), contexto.Plan.Modo, contexto.Paquete.Version,
            contexto.Paquete.HuellaContenido, huella, bytes, ReferenciaRetirada.Conservar(contexto.Plan.Solicitud),
            ReferenciaRetirada.Unir(anterior?.Modo == contexto.Plan.Modo ? anterior.Inventario : null, contexto.Paquete.Del(contexto.Plan.Modo)));
        referencia.Guardar(archivo);

        contexto.Sistema.Aplicaciones.Escribir(Esperada(contexto, carpeta, bytes));
        contexto.Propio(TipoArtefacto.RegistroAplicacion, clave);
        // La entrada ya apunta a la herramienta nueva: la copia con el nombre anterior al cambio de nombre no la usa nadie.
        var herramientaAnterior = Path.Combine(carpeta, ProgramasAnteriores.Herramienta);
        RutasSeguras.ExigirSinPuntosReparacion(herramientaAnterior);
        if (File.Exists(herramientaAnterior)) File.Delete(herramientaAnterior);
        // Una reinstalación completa vuelve a ser una instalación normal: la marca de datos retirados ya no aplica.
        if (contexto.Estado.Retirada is not null)
        {
            contexto.Estado.Retirada = null;
            contexto.Estado.Guardar(contexto.Plan.DirectorioRegistros);
        }
        return $"Herramienta protegida en {carpeta}; entrada «{clave}» en Aplicaciones instaladas.";
    }

    private static async Task<string?> DiferenciaAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var carpeta = ReferenciaRetirada.Carpeta(contexto.Plan.DirectorioRegistros);
        if (!contexto.Estado.EsPropio(TipoArtefacto.Herramienta, carpeta) || contexto.Sistema.Permisos.Leer(carpeta)?.Coincide(Acceso) != true)
            return "Falta la carpeta protegida de la herramienta de retirada.";
        if (contexto.Estado.Retirada is not null) return "Los datos proceden de una retirada: se vuelve a registrar la instalación.";
        var archivo = ReferenciaRetirada.Archivo(carpeta);
        if (!File.Exists(archivo)) return "Falta la referencia de retirada.";
        var referencia = ReferenciaRetirada.Leer(archivo);
        if (referencia.Modo != contexto.Plan.Modo || referencia.Version != contexto.Paquete.Version
            || referencia.HuellaManifiesto != contexto.Paquete.HuellaContenido)
            return "La referencia de retirada describe otro paquete.";
        if (await HuellaAsync(Path.Combine(carpeta, ReferenciaRetirada.NombreHerramienta), ct) is not var (bytes, huella)
            || bytes != referencia.HerramientaBytes || huella != referencia.HerramientaSha256)
            return "La herramienta protegida falta o no coincide con su referencia.";
        var entrada = contexto.Sistema.Aplicaciones.Leer(RegistroRetirada.Clave(contexto.Plan.Modo));
        return entrada == Esperada(contexto, carpeta, bytes) ? null : "La entrada de Aplicaciones instaladas falta o no coincide.";
    }

    private static EntradaAplicacion Esperada(ContextoEjecucion contexto, string carpeta, long bytes)
    {
        var herramienta = Path.Combine(carpeta, ReferenciaRetirada.NombreHerramienta);
        var nombre = contexto.Plan.Modo == ModoInstalacion.Servidor ? NombreSistema.Visible + " - Servidor" : NombreSistema.Visible + " - Estación";
        var programas = contexto.Paquete.Del(contexto.Plan.Modo).Sum(a => a.Bytes);
        return new EntradaAplicacion(RegistroRetirada.Clave(contexto.Plan.Modo), nombre, contexto.Paquete.Version, herramienta,
            contexto.Plan.Solicitud.DirectorioPrograma, RegistroRetirada.Comando(herramienta, ReferenciaRetirada.Archivo(carpeta)),
            (int)Math.Min(int.MaxValue, (programas + bytes) / 1024));
    }

    internal static async Task<(long Bytes, string Sha256)?> HuellaAsync(string ruta, CancellationToken ct)
    {
        RutasSeguras.ExigirSinPuntosReparacion(ruta);
        var info = new FileInfo(ruta);
        if (!info.Exists || info.Attributes.HasFlag(FileAttributes.ReparsePoint)) return null;
        await using var flujo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);
        return (info.Length, Convert.ToHexString(await SHA256.HashDataAsync(flujo, ct)));
    }
}
