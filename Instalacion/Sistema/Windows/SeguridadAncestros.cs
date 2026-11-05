using System.Security.AccessControl;
using System.Security.Principal;

namespace Clarisa.Instalacion.Sistema.Windows;

// Los servicios ejecutan programas y leen datos bajo estas carpetas con su propia cuenta. Si una identidad sin administración puede
// borrar, renombrar o cambiar permisos de cualquier carpeta superior, puede apartar la carpeta instalada y poner otra en su lugar.
// Solo cuentan los grupos Administradores, SYSTEM y TrustedInstaller: la cuenta personal de un administrador también ejecuta
// procesos sin elevar (token filtrado), así que su SID no es de confianza.
internal static class SeguridadAncestros
{
    private const int Borrar = 0x00010000;
    private const int EscribirPermisos = 0x00040000;
    private const int TomarPropiedad = 0x00080000;
    private const int BorrarContenido = 0x00000040;
    private const int GenericoTotal = 0x10000000;
    private const int PeligrososRaiz = EscribirPermisos | TomarPropiedad | BorrarContenido | GenericoTotal;
    private const int PeligrososAncestro = PeligrososRaiz | Borrar;
    private const int LecturaDirectorio = 0x001200a9;
    private const int GenericoLectura = unchecked((int)0x80000000);
    private const int GenericoEjecucion = 0x20000000;
    private const int SoloLectura = LecturaDirectorio | GenericoLectura | GenericoEjecucion;

    private static readonly SecurityIdentifier[] Confiables =
    [
        new(IdentidadServicio.Administradores), new(IdentidadServicio.Sistema),
        new("S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464")
    ];

    // CREATOR OWNER y CREATOR GROUP solo actúan como plantilla de herencia; OWNER RIGHTS es el propietario, que ya se exige confiable.
    private static readonly SecurityIdentifier[] Marcadores = [new("S-1-3-0"), new("S-1-3-1"), new("S-1-3-4")];

    public static string? Revisar(string ruta)
    {
        var completa = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ruta));
        var raiz = Path.GetPathRoot(completa);
        var padre = Path.GetDirectoryName(completa);
        if (string.IsNullOrEmpty(raiz) || padre is null) return "No se admite la raíz de una unidad.";
        if (!Directory.Exists(padre))
            return $"La carpeta {padre} no existe. Créala antes con permisos solo de administración o usa la ubicación predeterminada: " +
                   "el instalador no crea carpetas intermedias que heredarían permisos de escritura.";
        foreach (var ancestro in Ancestros(raiz, padre))
        {
            string? motivo;
            try
            {
                if (!ancestro.Equals(raiz, StringComparison.OrdinalIgnoreCase)
                    && File.GetAttributes(ancestro).HasFlag(FileAttributes.ReparsePoint))
                    return $"La ruta pasa por un enlace o junction no admitido: {ancestro}.";
                var seguridad = new DirectoryInfo(ancestro).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
                motivo = Evaluar(new RawSecurityDescriptor(seguridad.GetSecurityDescriptorBinaryForm(), 0),
                    ancestro.Equals(raiz, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception error) when (error is UnauthorizedAccessException or IOException or InvalidOperationException)
            {
                motivo = "no se pudieron leer sus permisos";
            }
            if (motivo is not null) return $"La carpeta {ancestro} no es segura: {motivo}.";
        }
        return RevisarDestino(completa);
    }

    // Una carpeta de destino que ya existe solo se usa si nadie sin administración pudo crearla, poseerla ni escribir en ella: quien
    // la creó o pudo modificarla conserva los identificadores abiertos antes de que el instalador cambie sus permisos.
    public static string? RevisarDestino(string ruta)
    {
        var completa = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ruta));
        if (File.Exists(completa)) return $"{completa} es un archivo, no una carpeta.";
        if (!Directory.Exists(completa)) return null;
        string? motivo;
        try
        {
            if (File.GetAttributes(completa).HasFlag(FileAttributes.ReparsePoint)) return $"La carpeta {completa} es un enlace o junction no admitido.";
            var seguridad = new DirectoryInfo(completa).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
            motivo = EvaluarDestino(new RawSecurityDescriptor(seguridad.GetSecurityDescriptorBinaryForm(), 0));
        }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException or InvalidOperationException)
        {
            motivo = "no se pudieron leer sus permisos";
        }
        return motivo is null ? null : $"La carpeta {completa} ya existe y no se adopta: {motivo}. Elige una carpeta que no exista (el " +
                                       "instalador la crea protegida) o pide a la administración que corrija su propietario y permisos.";
    }

    public static string? EvaluarDestino(RawSecurityDescriptor descriptor)
    {
        if (descriptor.Owner is null || !Confiables.Contains(descriptor.Owner))
            return $"su propietario es {Nombre(descriptor.Owner)}, no Administradores ni SYSTEM";
        if (descriptor.DiscretionaryAcl is null) return "no tiene lista de permisos, así que cualquiera puede modificarla";
        foreach (GenericAce entrada in descriptor.DiscretionaryAcl)
        {
            if (entrada.AceFlags.HasFlag(AceFlags.InheritOnly)) continue;
            if (entrada is not QualifiedAce calificada) return "tiene una entrada de permisos no reconocida";
            if (calificada.AceQualifier != AceQualifier.AccessAllowed) continue;
            var identidad = calificada.SecurityIdentifier;
            if (Confiables.Contains(identidad) || Marcadores.Contains(identidad)) continue;
            if ((calificada.AccessMask & ~SoloLectura) != 0) return $"{Nombre(identidad)} puede crear, modificar o borrar contenido en ella";
        }
        return null;
    }

    public static string? Evaluar(RawSecurityDescriptor descriptor, bool esRaiz)
    {
        if (descriptor.Owner is null || !Confiables.Contains(descriptor.Owner))
            return $"su propietario es {Nombre(descriptor.Owner)}, no Administradores ni SYSTEM";
        if (descriptor.DiscretionaryAcl is null) return "no tiene lista de permisos, así que cualquiera puede modificarla";
        foreach (GenericAce entrada in descriptor.DiscretionaryAcl)
        {
            if (entrada.AceFlags.HasFlag(AceFlags.InheritOnly)) continue;
            if (entrada is not QualifiedAce calificada) return "tiene una entrada de permisos no reconocida";
            if (calificada.AceQualifier != AceQualifier.AccessAllowed) continue;
            var identidad = calificada.SecurityIdentifier;
            if (Confiables.Contains(identidad) || Marcadores.Contains(identidad)) continue;
            var peligrosos = calificada.AccessMask & (esRaiz ? PeligrososRaiz : PeligrososAncestro);
            if (peligrosos != 0) return $"{Nombre(identidad)} puede {Describir(peligrosos)}";
        }
        return null;
    }

    private static IEnumerable<string> Ancestros(string raiz, string padre)
    {
        var actual = raiz;
        yield return actual;
        foreach (var parte in padre[raiz.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            actual = Path.Combine(actual, parte);
            yield return actual;
        }
    }

    private static string Describir(int derechos)
    {
        var partes = new List<string>();
        if ((derechos & (Borrar | GenericoTotal)) != 0) partes.Add("borrarla o renombrarla");
        if ((derechos & (BorrarContenido | GenericoTotal)) != 0) partes.Add("borrar o renombrar lo que contiene");
        if ((derechos & (EscribirPermisos | GenericoTotal)) != 0) partes.Add("cambiar sus permisos");
        if ((derechos & (TomarPropiedad | GenericoTotal)) != 0) partes.Add("tomar su propiedad");
        return string.Join(", ", partes);
    }

    private static string Nombre(SecurityIdentifier? identidad)
    {
        if (identidad is null) return "desconocido";
        try
        {
            return identidad.Translate(typeof(NTAccount)).Value;
        }
        catch (SystemException)
        {
            return identidad.Value;
        }
    }
}
