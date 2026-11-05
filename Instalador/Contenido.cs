using System.IO;
using System.IO.Compression;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Clarisa.Instalador;

// Descomprime la carpeta del instalador que viaja dentro de este programa.
internal static class Contenido
{
    private const string Recurso = "Clarisa.Instalador.Contenido.zip";
    private const long EspacioNecesario = 1500L * 1024 * 1024;

    public static Stream? Abrir() => typeof(Contenido).Assembly.GetManifestResourceStream(Recurso);

    // Carpeta nueva con nombre al azar en ProgramData (no dentro de la carpeta de Windows: el instalador no acepta paquetes de ahí).
    // Nace con permisos solo para administradores y el sistema, así nadie sin permisos puede cambiar un archivo entre la
    // descompresión y la instalación.
    public static string CrearCarpeta()
    {
        var raiz = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var unidad = new DriveInfo(Path.GetPathRoot(raiz)!);
        if (unidad.AvailableFreeSpace < EspacioNecesario)
            throw new IOException($"Hace falta al menos 1,5 GB libres en el disco {unidad.Name} para preparar la instalación.");
        var carpeta = Path.Combine(raiz, "Clarisa-Instalador-" + Guid.NewGuid().ToString("N"));
        var seguridad = new DirectorySecurity();
        seguridad.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        var administradores = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        seguridad.SetOwner(administradores);
        foreach (var identidad in new[] { administradores, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
            seguridad.AddAccessRule(new FileSystemAccessRule(identidad, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(carpeta).Create(seguridad);
        return carpeta;
    }

    public static void Descomprimir(Stream origen, string carpeta, IProgress<double> avance, CancellationToken ct)
    {
        using var archivo = new ZipArchive(origen, ZipArchiveMode.Read);
        var raiz = Path.GetFullPath(carpeta) + Path.DirectorySeparatorChar;
        var total = Math.Max(1, archivo.Entries.Sum(e => e.Length));
        long hecho = 0;
        var bufer = new byte[1 << 20];
        foreach (var entrada in archivo.Entries)
        {
            ct.ThrowIfCancellationRequested();
            var destino = Path.GetFullPath(Path.Combine(carpeta, entrada.FullName));
            if (!destino.StartsWith(raiz, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("El instalador está dañado: contiene una ruta no válida.");
            if (entrada.FullName.EndsWith('/')) { Directory.CreateDirectory(destino); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
            using var lectura = entrada.Open();
            using var escritura = new FileStream(destino, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16);
            int leidos;
            while ((leidos = lectura.Read(bufer, 0, bufer.Length)) > 0)
            {
                escritura.Write(bufer, 0, leidos);
                hecho += leidos;
                avance.Report(hecho * 100.0 / total);
            }
        }
    }

    public static void Borrar(string carpeta)
    {
        for (var intento = 0; intento < 5 && Directory.Exists(carpeta); intento++)
        {
            try { Directory.Delete(carpeta, recursive: true); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Thread.Sleep(1000); }
        }
    }
}
