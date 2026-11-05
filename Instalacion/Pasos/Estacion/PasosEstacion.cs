using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Planificacion;
using Clarisa.Instalacion.Registro;

namespace Clarisa.Instalacion.Pasos.Estacion;

internal sealed class PasoConfianzaCertificado : IPasoInstalacion
{
    public string Id => "confianza-certificado";
    public string Nombre => "Confianza en el certificado del servidor";
    public string Descripcion => "Confía únicamente en el certificado del servidor cuya huella coincide con la indicada.";
    public bool Critico => true;
    public bool Privilegiado => true;

    public Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        using var certificado = CargarValidado(contexto);
        var huella = Huella(certificado);
        return Task.FromResult(contexto.Sistema.Confianza.Contiene(huella)
            ? Comprobacion.Satisfecha($"Certificado {huella[..16]}… presente en las raíces de confianza del equipo.")
            : Comprobacion.NoSatisfecha("El certificado del servidor todavía no es de confianza en este equipo."));
    }

    public Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        using var certificado = CargarValidado(contexto);
        contexto.Sistema.Confianza.Agregar(certificado);
        contexto.Propio(TipoArtefacto.CertificadoConfianza, Huella(certificado));
        return Task.CompletedTask;
    }

    // Antes de confiar: huella exacta, vigencia, uso de servidor, sin capacidad de autoridad y nombre del servidor en el SAN.
    internal static X509Certificate2 CargarValidado(ContextoEjecucion contexto)
    {
        var solicitud = contexto.Estacion;
        X509Certificate2 certificado;
        try
        {
            certificado = X509CertificateLoader.LoadCertificateFromFile(solicitud.ArchivoCertificado);
        }
        catch (CryptographicException)
        {
            throw new FalloInstalacionException("El archivo indicado no es un certificado válido.");
        }
        var problema = Problema(certificado, ValidadorSolicitud.NormalizarHuella(solicitud.HuellaCertificado),
            ValidadorSolicitud.DireccionEstacion(solicitud.DireccionServidor)!, contexto.Sistema.Reloj());
        if (problema is null) return certificado;
        certificado.Dispose();
        throw new FalloInstalacionException(problema);
    }

    internal static string? Problema(X509Certificate2 certificado, string huellaEsperada, Uri servidor, DateTimeOffset ahora)
    {
        if (!string.Equals(Huella(certificado), huellaEsperada, StringComparison.Ordinal))
            return "La huella del certificado no coincide con la indicada. No se confía en él.";
        if (ahora < certificado.NotBefore || ahora > certificado.NotAfter) return "El certificado no está vigente.";
        if (certificado.HasPrivateKey) return "El archivo contiene una clave privada; usa solo el certificado público exportado.";
        if (certificado.Extensions.OfType<X509BasicConstraintsExtension>().Any(b => b.CertificateAuthority))
            return "El certificado permite emitir otros certificados; no se instala como confianza.";
        if (!certificado.MatchesHostname(servidor.IdnHost, allowWildcards: false))
            return $"El certificado no incluye el nombre {servidor.IdnHost} en sus nombres alternativos.";
        return null;
    }

    internal static string Huella(X509Certificate2 certificado) => Convert.ToHexString(certificado.GetCertHash(HashAlgorithmName.SHA256));
}

internal sealed class PasoConexionServidor : IPasoInstalacion
{
    public string Id => "conexion-servidor";
    public string Nombre => "Conexión HTTPS con el servidor";
    public string Descripcion => "Conecta con el servidor usando la confianza del sistema y comprueba que responde la API del liceo.";
    public bool Critico => true;
    public bool Privilegiado => false;

    public async Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var servidor = ValidadorSolicitud.DireccionEstacion(contexto.Estacion.DireccionServidor)!;
        try
        {
            var respuesta = await contexto.Sistema.Cliente.ConsultarSesionAnonimaAsync(servidor, null, ct);
            return respuesta.Estado == HttpStatusCode.Unauthorized
                ? Comprobacion.Satisfecha($"El servidor {servidor.Authority} respondió por HTTPS con certificado de confianza.")
                : Comprobacion.NoSatisfecha("El servidor respondió de forma inesperada: " + respuesta.Detalle);
        }
        catch (HttpRequestException error)
        {
            return Comprobacion.Indeterminada($"No se pudo comprobar la conexión con {servidor.Authority}: " +
                                              (error.InnerException?.Message ?? error.Message) + " Revisa red, nombre y firewall del servidor.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Comprobacion.Indeterminada($"El servidor {servidor.Authority} no respondió a tiempo.");
        }
    }

    public Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class PasoAccesoDirecto : IPasoInstalacion
{
    public string Id => "acceso-directo";
    public string Nombre => "Acceso directo con la dirección del servidor";
    public string Descripcion => "Crea el acceso directo a la aplicación con --servidor y la dirección validada; sin claves ni cookies.";
    public bool Critico => true;
    public bool Privilegiado => true;

    public Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var (destino, argumentos) = Esperado(contexto);
        var faltan = Archivos(contexto).Where(a => contexto.Sistema.Accesos.Leer(a) is not { } leido
            || !string.Equals(leido.Destino, destino, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(leido.Argumentos, argumentos, StringComparison.Ordinal)).ToList();
        faltan.AddRange(Anteriores(contexto));
        return Task.FromResult(faltan.Count == 0
            ? Comprobacion.Satisfecha($"Accesos directos verificados: {argumentos}")
            : Comprobacion.NoSatisfecha("Falta o no coincide: " + string.Join(", ", faltan.Select(Path.GetFileName))));
    }

    public Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var (destino, argumentos) = Esperado(contexto);
        if (!File.Exists(destino)) throw new FalloInstalacionException("No se encuentra la aplicación instalada para el acceso directo.");
        foreach (var archivo in Archivos(contexto))
        {
            RutasSeguras.ExigirSinPuntosReparacion(archivo);
            Directory.CreateDirectory(Path.GetDirectoryName(archivo)!);
            contexto.Sistema.Accesos.Crear(archivo, destino, argumentos, Path.GetDirectoryName(destino)!, NombreSistema.Visible);
            contexto.Propio(TipoArtefacto.AccesoDirecto, archivo);
        }
        foreach (var anterior in Anteriores(contexto))
        {
            RutasSeguras.ExigirSinPuntosReparacion(anterior);
            File.Delete(anterior);
            contexto.Estado.OlvidarArtefacto(TipoArtefacto.AccesoDirecto, anterior);
        }
        contexto.Estado.Guardar(contexto.Plan.DirectorioRegistros);
        return Task.CompletedTask;
    }

    // Accesos «Clarisa Estudios.lnk» que creó esta instalación antes del cambio de nombre visible y que siguen apuntando a la
    // aplicación: se sustituyen por los nuevos. Uno modificado por otra persona no se toca.
    private static List<string> Anteriores(ContextoEjecucion contexto)
    {
        var carpeta = Path.Combine(contexto.Estacion.DirectorioPrograma, "estacion");
        return contexto.Estado.Artefactos.Where(a => a.Tipo == TipoArtefacto.AccesoDirecto
                && string.Equals(Path.GetFileName(a.Identificador), NombreSistema.AccesoDirectoAnterior, StringComparison.OrdinalIgnoreCase)
                && File.Exists(a.Identificador))
            .Select(a => a.Identificador)
            .Where(a => contexto.Sistema.Accesos.Leer(a) is { } leido && leido.Argumentos.StartsWith("--servidor ", StringComparison.Ordinal)
                        && ProgramasAnteriores.MismoArchivo(leido.Destino, Path.Combine(carpeta, "Clarisa.Desktop.exe"),
                            Path.Combine(carpeta, ProgramasAnteriores.Escritorio)))
            .ToList();
    }

    private static (string Destino, string Argumentos) Esperado(ContextoEjecucion contexto)
    {
        var servidor = ValidadorSolicitud.DireccionEstacion(contexto.Estacion.DireccionServidor)
                       ?? throw new FalloInstalacionException("La dirección del servidor no es válida.");
        return (Path.Combine(contexto.Estacion.DirectorioPrograma, "estacion", "Clarisa.Desktop.exe"), $"--servidor {servidor.AbsoluteUri}");
    }

    private static IReadOnlyList<string> Archivos(ContextoEjecucion contexto)
    {
        var equipo = contexto.Sistema.Equipo;
        var lista = new List<string>();
        if (equipo.DirectorioMenuInicioComun() is { } menu) lista.Add(Path.Combine(menu, NombreSistema.AccesoDirecto));
        if (contexto.Estacion.AccesoEnEscritorio && equipo.DirectorioEscritorioComun() is { } escritorio)
            lista.Add(Path.Combine(escritorio, NombreSistema.AccesoDirecto));
        if (lista.Count == 0) throw new FalloInstalacionException("No se encontraron las carpetas comunes de Windows para el acceso directo.");
        return lista;
    }
}
