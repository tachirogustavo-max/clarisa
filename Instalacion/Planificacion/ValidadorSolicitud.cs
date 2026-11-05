using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Clarisa.Instalacion.Contratos;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Planificacion;

internal sealed partial class ValidadorSolicitud(IEquipo equipo)
{
    private const int MaximoRuta = 180;
    private readonly List<ProblemaSolicitud> problemas = [];

    public IReadOnlyList<ProblemaSolicitud> Validar(SolicitudInstalacion solicitud)
    {
        problemas.Clear();
        Directorio(nameof(solicitud.OrigenPaquete), solicitud.OrigenPaquete, debeExistir: true);
        Directorio(nameof(solicitud.DirectorioPrograma), solicitud.DirectorioPrograma, debeExistir: false, destino: true);
        Separadas(nameof(solicitud.DirectorioPrograma), solicitud.OrigenPaquete, solicitud.DirectorioPrograma,
            "El paquete de instalación y la carpeta de programas deben ser distintas.");
        switch (solicitud)
        {
            case SolicitudServidor servidor: ValidarServidor(servidor); break;
            case SolicitudEstacion estacion: ValidarEstacion(estacion); break;
        }
        return problemas.ToList();
    }

    private void ValidarServidor(SolicitudServidor s)
    {
        Directorio(nameof(s.DirectorioDatos), s.DirectorioDatos, debeExistir: false, destino: true);
        Separadas(nameof(s.DirectorioDatos), s.DirectorioDatos, s.DirectorioPrograma,
            "Los datos del servidor y los programas deben estar en carpetas separadas.");
        Separadas(nameof(s.DirectorioDatos), s.DirectorioDatos, s.OrigenPaquete, "El paquete de instalación no puede estar en la carpeta de datos.");
        if (!NombreValido(s.NombreConexion))
            Problema(nameof(s.NombreConexion), "Escribe el nombre del equipo en la red local o su IP fija (no localhost).");
        if (s.DireccionEscucha is null || s.DireccionEscucha.Equals(IPAddress.Any) || s.DireccionEscucha.Equals(IPAddress.IPv6Any)
            || IPAddress.IsLoopback(s.DireccionEscucha))
            Problema(nameof(s.DireccionEscucha), "Elige la dirección de la tarjeta de red local concreta; no se escucha en todas.");
        else if (!equipo.DireccionLocal(s.DireccionEscucha))
            Problema(nameof(s.DireccionEscucha), "Esa dirección no pertenece a este equipo.");
        else if (IPAddress.TryParse(s.NombreConexion, out var ip) && !ip.Equals(s.DireccionEscucha))
            Problema(nameof(s.NombreConexion), "La IP de conexión debe ser la misma dirección en la que escucha el servidor.");
        Puerto(nameof(s.PuertoHttps), s.PuertoHttps);
        Puerto(nameof(s.PuertoMotor), s.PuertoMotor);
        if (s.PuertoHttps == s.PuertoMotor) Problema(nameof(s.PuertoMotor), "El motor de datos necesita un puerto distinto al del servidor.");
        if (string.IsNullOrWhiteSpace(s.UsuarioPrimeraCuenta) || !Usuario().IsMatch(s.UsuarioPrimeraCuenta))
            Problema(nameof(s.UsuarioPrimeraCuenta), "Usa de 3 a 40 letras, números, punto, guion o guion bajo.");
        ValidarDestinoClave(s);
    }

    private void ValidarDestinoClave(SolicitudServidor s)
    {
        const string campo = nameof(s.DestinoClaveRecuperacion);
        if (!EsAbsoluta(s.DestinoClaveRecuperacion) || s.DestinoClaveRecuperacion.Length > MaximoRuta)
        {
            Problema(campo, "Indica la ruta completa del archivo de recuperación (por ejemplo, en una memoria USB).");
            return;
        }
        if (EsAbsoluta(s.DirectorioDatos) && RutasSeguras.Contiene(s.DirectorioDatos, s.DestinoClaveRecuperacion)
            || EsAbsoluta(s.DirectorioPrograma) && RutasSeguras.Contiene(s.DirectorioPrograma, s.DestinoClaveRecuperacion))
            Problema(campo, "La clave de recuperación no puede guardarse junto a los datos ni los programas del servidor.");
        else if (Path.GetDirectoryName(s.DestinoClaveRecuperacion) is not { } carpeta || !Directory.Exists(carpeta))
            Problema(campo, "La carpeta de destino no existe o la unidad no está conectada.");
        else if (MismaUnidad(s.DestinoClaveRecuperacion, s.DirectorioDatos, s.DirectorioPrograma,
                     Environment.GetFolderPath(Environment.SpecialFolder.Windows)))
            Problema(campo, "Usa una memoria USB u otra unidad distinta del disco del servidor: la clave no debe quedar en este equipo.");
        else if (File.Exists(s.DestinoClaveRecuperacion))
            Problema(campo, "Ya existe un archivo con ese nombre; elige uno nuevo para no sustituir otra clave.");
    }

    private bool MismaUnidad(string destino, params string[] otras)
    {
        var unidad = equipo.Unidad(destino);
        return otras.Where(EsAbsoluta).Any(o => string.Equals(equipo.Unidad(o), unidad, StringComparison.OrdinalIgnoreCase));
    }

    private void Separadas(string campo, string? a, string? b, string mensaje)
    {
        if (EsAbsoluta(a) && EsAbsoluta(b) && (RutasSeguras.Contiene(a!, b!) || RutasSeguras.Contiene(b!, a!))) Problema(campo, mensaje);
    }

    private void ValidarEstacion(SolicitudEstacion s)
    {
        if (DireccionEstacion(s.DireccionServidor) is null)
            Problema(nameof(s.DireccionServidor), "Escribe la dirección HTTPS del servidor, por ejemplo https://servidor-liceo:5189/, sin rutas ni claves.");
        if (!EsAbsoluta(s.ArchivoCertificado) || !File.Exists(s.ArchivoCertificado))
            Problema(nameof(s.ArchivoCertificado), "Selecciona el certificado exportado por el servidor.");
        else if (new FileInfo(s.ArchivoCertificado).Length > 16 * 1024)
            Problema(nameof(s.ArchivoCertificado), "El archivo no parece un certificado del servidor.");
        if (string.IsNullOrWhiteSpace(s.HuellaCertificado) || !Huella().IsMatch(NormalizarHuella(s.HuellaCertificado)))
            Problema(nameof(s.HuellaCertificado), "Copia la huella SHA-256 que muestra el resumen del servidor (64 caracteres).");
    }

    public static Uri? DireccionEstacion(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Length > 250 || !Uri.TryCreate(texto.Trim(), UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0
            || uri.AbsolutePath != "/" || uri.IsLoopback) return null;
        return uri;
    }

    public static string NormalizarHuella(string huella) =>
        new string(huella.Where(c => !char.IsWhiteSpace(c) && c != ':').ToArray()).ToUpperInvariant();

    private static bool NombreValido(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 63 * 4) return false;
        if (IPAddress.TryParse(nombre, out var ip))
            return !IPAddress.IsLoopback(ip) && !ip.Equals(IPAddress.Any) && ip.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6;
        return Uri.CheckHostName(nombre) == UriHostNameType.Dns && !nombre.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               && NombreDns().IsMatch(nombre);
    }

    // Los destinos de programas y datos se validan además por sus carpetas superiores: protegerlos solo a ellos no basta.
    private void Directorio(string campo, string? ruta, bool debeExistir, bool destino = false)
    {
        if (!EsAbsoluta(ruta) || ruta!.Length > MaximoRuta || ruta.Any(char.IsControl))
            Problema(campo, "Indica una carpeta con ruta completa y de longitud razonable.");
        else if (destino && !EnDiscoLocal(ruta))
            Problema(campo, "Usa una carpeta de un disco de este equipo, como C:\\…; no una ruta de red ni de dispositivo.");
        else if (Path.GetPathRoot(Path.GetFullPath(ruta)) is { } raiz && Path.TrimEndingDirectorySeparator(Path.GetFullPath(ruta))
                     .Equals(Path.TrimEndingDirectorySeparator(raiz), StringComparison.OrdinalIgnoreCase))
            Problema(campo, "No se usa la raíz de una unidad.");
        else if (debeExistir && !Directory.Exists(ruta))
            Problema(campo, "La carpeta no existe.");
        else if (EsSistema(ruta))
            Problema(campo, "No se instala dentro de la carpeta de Windows.");
        else if (destino && equipo.AncestroInseguro(ruta) is { } motivo)
            Problema(campo, motivo + " Un usuario sin administración podría sustituir lo instalado. Usa la ubicación predeterminada o una " +
                            "carpeta cuyas carpetas superiores solo pueda modificar la administración.");
    }

    private static bool EnDiscoLocal(string ruta) =>
        ruta.Length >= 3 && char.IsAsciiLetter(ruta[0]) && ruta[1] == ':' && ruta[2] is '\\' or '/';

    private static bool EsSistema(string ruta) =>
        Environment.GetFolderPath(Environment.SpecialFolder.Windows) is { Length: > 0 } windows && RutasSeguras.Contiene(windows, ruta);

    private void Puerto(string campo, int puerto)
    {
        if (puerto is < 1024 or > 65535) Problema(campo, "Usa un puerto entre 1024 y 65535.");
    }

    private static bool EsAbsoluta(string? ruta) => !string.IsNullOrWhiteSpace(ruta) && Path.IsPathFullyQualified(ruta);

    private void Problema(string campo, string mensaje) => problemas.Add(new ProblemaSolicitud(campo, mensaje));

    [GeneratedRegex("^[A-Za-z0-9._-]{3,40}$")]
    private static partial Regex Usuario();

    [GeneratedRegex("^[0-9A-F]{64}$")]
    private static partial Regex Huella();

    [GeneratedRegex("^[A-Za-z0-9-]+(\\.[A-Za-z0-9-]+)*$")]
    private static partial Regex NombreDns();
}
