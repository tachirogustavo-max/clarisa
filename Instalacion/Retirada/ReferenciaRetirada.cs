using System.Net;
using System.Text.Json;
using Clarisa.Instalacion.Contratos;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Paquete;
using Clarisa.Instalacion.Registro;

namespace Clarisa.Instalacion.Retirada;

internal sealed record HuellaArchivo(long Bytes, string Sha256);

internal sealed record EntradaInventario(string Ruta, List<HuellaArchivo> Versiones);

// Datos de la solicitud original sin secretos: bastan para reconstruir las rutas y los nombres que se retiran.
internal sealed record SolicitudConservada(string Programa, string? Datos, string? NombreConexion, string? DireccionEscucha, int PuertoHttps,
    int PuertoMotor, string? DestinoClave, string? Usuario, string? DireccionServidor, string? ArchivoCertificado, string? HuellaCertificado,
    bool AccesoEnEscritorio);

internal sealed record ReferenciaRetirada(int Formato, Guid Instalacion, ModoInstalacion Modo, string Version, string HuellaManifiesto,
    string HerramientaSha256, long HerramientaBytes, SolicitudConservada Solicitud, List<EntradaInventario> Inventario)
{
    public const string NombreHerramienta = "Clarisa.Setup.exe";
    public const string NombreMantenimiento = "Clarisa.Mantenimiento.exe";
    private const long MaximoBytes = 16 * 1024 * 1024;
    private const int MaximoEntradas = 40_000;
    private const int MaximoVersiones = 8;

    // La caché vive junto a los registros: misma carpeta superior protegida, distinta de programas y de la API.
    public static string Carpeta(string directorioRegistros) =>
        Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(directorioRegistros)))!, "retirada");

    public static string Archivo(string carpeta) => Path.Combine(carpeta, ArgumentosRetirada.NombreReferencia);

    public SolicitudInstalacion Reconstruir(string origen) => Modo == ModoInstalacion.Servidor
        ? new SolicitudServidor(origen, Solicitud.Programa, Solicitud.Datos!, Solicitud.NombreConexion!, IPAddress.Parse(Solicitud.DireccionEscucha!),
            Solicitud.PuertoHttps, Solicitud.PuertoMotor, Solicitud.DestinoClave ?? "", Solicitud.Usuario!)
        : new SolicitudEstacion(origen, Solicitud.Programa, Solicitud.DireccionServidor!, Solicitud.ArchivoCertificado ?? "",
            Solicitud.HuellaCertificado ?? "", Solicitud.AccesoEnEscritorio);

    public static SolicitudConservada Conservar(SolicitudInstalacion solicitud) => solicitud switch
    {
        SolicitudServidor s => new(s.DirectorioPrograma, s.DirectorioDatos, s.NombreConexion, s.DireccionEscucha.ToString(), s.PuertoHttps,
            s.PuertoMotor, s.DestinoClaveRecuperacion, s.UsuarioPrimeraCuenta, null, null, null, false),
        SolicitudEstacion e => new(e.DirectorioPrograma, null, null, null, 0, 0, null, null, e.DireccionServidor, e.ArchivoCertificado,
            e.HuellaCertificado, e.AccesoEnEscritorio),
        _ => throw new ArgumentOutOfRangeException(nameof(solicitud))
    };

    // Une el inventario anterior con los archivos del paquete actual: tras actualizaciones pueden quedar archivos de versiones previas.
    public static List<EntradaInventario> Unir(IReadOnlyList<EntradaInventario>? anterior, IReadOnlyList<ArchivoPaquete> actuales)
    {
        var mapa = (anterior ?? []).ToDictionary(e => e.Ruta, e => e.Versiones.ToList(), StringComparer.OrdinalIgnoreCase);
        foreach (var archivo in actuales)
        {
            var versiones = mapa.TryGetValue(archivo.Ruta, out var lista) ? lista : mapa[archivo.Ruta] = [];
            versiones.RemoveAll(v => v.Bytes == archivo.Bytes && string.Equals(v.Sha256, archivo.Sha256, StringComparison.OrdinalIgnoreCase));
            versiones.Insert(0, new HuellaArchivo(archivo.Bytes, archivo.Sha256));
            if (versiones.Count > MaximoVersiones) versiones.RemoveRange(MaximoVersiones, versiones.Count - MaximoVersiones);
        }
        if (mapa.Count > MaximoEntradas) throw new FalloInstalacionException("El inventario de programas supera el límite admitido.");
        return mapa.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new EntradaInventario(p.Key, p.Value)).ToList();
    }

    public void Guardar(string ruta) => EscrituraSegura.Reemplazar(ruta,
        flujo => JsonSerializer.Serialize(flujo, this, new JsonSerializerOptions { WriteIndented = true }));

    public static ReferenciaRetirada Leer(string ruta)
    {
        RutasSeguras.ExigirSinPuntosReparacion(ruta);
        var info = new FileInfo(ruta);
        if (!info.Exists) throw new FalloInstalacionException("No existe la referencia de retirada indicada.");
        if (info.Length > MaximoBytes) throw new FalloInstalacionException("La referencia de retirada es demasiado grande.");
        ReferenciaRetirada? referencia;
        try
        {
            referencia = JsonSerializer.Deserialize<ReferenciaRetirada>(File.ReadAllBytes(info.FullName));
        }
        catch (JsonException error)
        {
            throw new FalloInstalacionException("La referencia de retirada está dañada; no se retira nada a ciegas.", error);
        }
        if (referencia is null || referencia.Formato != 1 || referencia.Instalacion == Guid.Empty || referencia.Solicitud is null
            || referencia.Inventario is null || referencia.Inventario.Count > MaximoEntradas || string.IsNullOrEmpty(referencia.Version)
            || !EsHuella(referencia.HuellaManifiesto) || !EsHuella(referencia.HerramientaSha256)
            || !Path.IsPathFullyQualified(referencia.Solicitud.Programa ?? "")
            || referencia.Inventario.Any(e => e is null || ManifiestoPaquete.ProblemaRuta(e.Ruta) is not null || e.Versiones is null
                                              || e.Versiones.Count is 0 or > MaximoVersiones || e.Versiones.Any(v => v is null || v.Bytes < 0 || !EsHuella(v.Sha256))))
            throw new FalloInstalacionException("La referencia de retirada tiene un formato no admitido.");
        if (referencia.Modo == ModoInstalacion.Servidor && (!Path.IsPathFullyQualified(referencia.Solicitud.Datos ?? "")
            || string.IsNullOrEmpty(referencia.Solicitud.NombreConexion) || string.IsNullOrEmpty(referencia.Solicitud.Usuario)
            || !IPAddress.TryParse(referencia.Solicitud.DireccionEscucha, out _)))
            throw new FalloInstalacionException("La referencia de retirada del servidor está incompleta.");
        if (referencia.Modo == ModoInstalacion.Estacion && string.IsNullOrEmpty(referencia.Solicitud.DireccionServidor))
            throw new FalloInstalacionException("La referencia de retirada de la estación está incompleta.");
        return referencia;
    }

    private static bool EsHuella(string? valor) => valor is { Length: 64 } && valor.All(Uri.IsHexDigit);
}
