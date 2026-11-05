using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

namespace Clarisa.Instalacion.Sistema.Windows;

internal sealed partial class PermisosNtfs : IPermisos
{
    private const int YaExiste = 183;

    private static readonly IReadOnlyDictionary<DerechoAcceso, FileSystemRights> Derechos = new Dictionary<DerechoAcceso, FileSystemRights>
    {
        [DerechoAcceso.Lectura] = FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize,
        [DerechoAcceso.Modificacion] = FileSystemRights.Modify | FileSystemRights.Synchronize,
        [DerechoAcceso.Control] = FileSystemRights.FullControl
    };

    public void Restringir(string ruta, IReadOnlyList<ReglaAcceso> reglas)
    {
        var seguridad = new DirectorySecurity();
        seguridad.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        seguridad.SetOwner(new SecurityIdentifier(IdentidadServicio.Administradores));
        foreach (var regla in reglas)
            seguridad.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(regla.Identidad), Derechos[regla.Derecho],
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        // Una carpeta nueva nace ya protegida: no pasa ningún instante con los permisos heredados del padre. Si otro la crea entre la
        // comprobación y la creación, la creación falla en lugar de adoptarla.
        var directorio = new DirectoryInfo(ruta);
        if (!directorio.Exists) CrearNueva(directorio.FullName, seguridad);
        directorio.SetAccessControl(seguridad);
    }

    internal static unsafe void CrearNueva(string ruta, DirectorySecurity seguridad)
    {
        var descriptor = seguridad.GetSecurityDescriptorBinaryForm();
        fixed (byte* puntero = descriptor)
        {
            var atributos = new AtributosSeguridad { Longitud = sizeof(AtributosSeguridad), Descriptor = (nint)puntero };
            if (CreateDirectoryW(ruta, ref atributos)) return;
        }
        var error = Marshal.GetLastPInvokeError();
        if (error == YaExiste)
            throw new IOException($"La carpeta {ruta} apareció mientras el instalador la creaba; no se adopta. Revisa quién la creó y vuelve a ejecutar.");
        throw new System.ComponentModel.Win32Exception(error);
    }

    public AclLeida? Leer(string ruta)
    {
        var directorio = new DirectoryInfo(ruta);
        if (!directorio.Exists) return null;
        var seguridad = directorio.GetAccessControl();
        var propietario = seguridad.GetOwner(typeof(SecurityIdentifier))?.Value ?? "";
        return new AclLeida(propietario, seguridad.AreAccessRulesProtected ? Reglas(seguridad) : null);
    }

    private static List<ReglaAcceso>? Reglas(DirectorySecurity seguridad)
    {
        var resultado = new List<ReglaAcceso>();
        const InheritanceFlags herencia = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        foreach (FileSystemAccessRule regla in seguridad.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            if (regla.AccessControlType != AccessControlType.Allow || regla.InheritanceFlags != herencia
                || regla.PropagationFlags != PropagationFlags.None) return null;
            var derecho = Derechos.FirstOrDefault(d => d.Value == regla.FileSystemRights);
            if (derecho.Value == 0) return null;
            resultado.Add(new ReglaAcceso(regla.IdentityReference.Value, derecho.Key));
        }
        return resultado;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AtributosSeguridad
    {
        public int Longitud;
        public nint Descriptor;
        public int Heredable;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateDirectoryW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreateDirectoryW(string ruta, ref AtributosSeguridad atributos);
}

internal sealed class AlmacenConfianzaEquipo : IAlmacenConfianza
{
    public bool Contiene(string huellaSha256)
    {
        using var almacen = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        almacen.Open(OpenFlags.ReadOnly);
        return almacen.Certificates.Any(c =>
        {
            using (c) return Convert.ToHexString(c.GetCertHash(HashAlgorithmName.SHA256)) == huellaSha256;
        });
    }

    public void Agregar(X509Certificate2 certificado)
    {
        using var almacen = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        almacen.Open(OpenFlags.ReadWrite);
        almacen.Add(certificado);
    }
}

internal sealed class ProteccionDpapiEquipo : IProteccionLocal
{
    private static readonly byte[] Entropia = "Clarisa.Estudios.Instalacion.Motor"u8.ToArray();

    public byte[] Proteger(byte[] datos) => ProtectedData.Protect(datos, Entropia, DataProtectionScope.LocalMachine);

    public byte[] Desproteger(byte[] datos) => ProtectedData.Unprotect(datos, Entropia, DataProtectionScope.LocalMachine);
}

internal sealed class EquipoWindows : IEquipo
{
    public DatosEquipo Describir()
    {
        using var identidad = WindowsIdentity.GetCurrent();
        return new(Environment.Is64BitOperatingSystem && RuntimeInformation.OSArchitecture == Architecture.X64,
            Environment.OSVersion.Version, new WindowsPrincipal(identidad).IsInRole(WindowsBuiltInRole.Administrator));
    }

    public string Unidad(string ruta) => (Path.GetPathRoot(Path.GetFullPath(ruta)) ?? "").TrimEnd('\\').ToUpperInvariant();

    public long BytesLibres(string ruta)
    {
        var raiz = Path.GetPathRoot(Path.GetFullPath(ruta)) ?? throw new IOException("Ruta sin unidad.");
        var unidad = new DriveInfo(raiz);
        if (!unidad.IsReady) throw new Ejecucion.FalloInstalacionException($"La unidad {raiz} no está disponible.");
        return unidad.AvailableFreeSpace;
    }

    public bool PuertoDisponible(IPAddress direccion, int puerto)
    {
        var activos = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
        if (activos.Any(e => e.Port == puerto && (e.Address.Equals(direccion) || e.Address.Equals(IPAddress.Any) || e.Address.Equals(IPAddress.IPv6Any))))
            return false;
        try
        {
            using var prueba = new TcpListener(direccion, puerto) { ExclusiveAddressUse = true };
            prueba.Start();
            prueba.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    public bool DireccionLocal(IPAddress direccion) => NetworkInterface.GetAllNetworkInterfaces()
        .Where(i => i.OperationalStatus == OperationalStatus.Up)
        .SelectMany(i => i.GetIPProperties().UnicastAddresses)
        .Any(u => u.Address.Equals(direccion));

    public string? AncestroInseguro(string ruta) => SeguridadAncestros.Revisar(ruta);

    public string? DirectorioEscritorioComun() => Carpeta(Environment.SpecialFolder.CommonDesktopDirectory);

    public string? DirectorioMenuInicioComun() =>
        Carpeta(Environment.SpecialFolder.CommonPrograms) is { } programas ? programas : null;

    private static string? Carpeta(Environment.SpecialFolder carpeta) =>
        Environment.GetFolderPath(carpeta) is { Length: > 0 } ruta ? ruta : null;
}
