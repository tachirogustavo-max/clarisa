using System.Security.Cryptography;
using System.Text;

namespace Clarisa.Instalacion.Sistema;

// SID de servicio (S-1-5-80-…): Windows lo deriva del nombre con SHA-1 sobre el nombre en mayúsculas UTF-16. Calcularlo permite
// preparar las ACL antes de registrar el servicio y compararlas sin depender de una búsqueda de cuentas.
internal static class IdentidadServicio
{
    public const string Administradores = "S-1-5-32-544";
    public const string Sistema = "S-1-5-18";
    public const string Usuarios = "S-1-5-32-545";

    public static string Cuenta(string servicio) => @"NT SERVICE\" + servicio;

    public static string Sid(string servicio)
    {
#pragma warning disable CA5350
        var huella = SHA1.HashData(Encoding.Unicode.GetBytes(servicio.ToUpperInvariant()));
#pragma warning restore CA5350
        var partes = Enumerable.Range(0, 5).Select(i => BitConverter.ToUInt32(huella, i * 4));
        return "S-1-5-80-" + string.Join('-', partes);
    }
}
