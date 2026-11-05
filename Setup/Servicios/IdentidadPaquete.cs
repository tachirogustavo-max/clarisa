using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace Clarisa.Setup.Servicios;

internal static class IdentidadPaquete
{
    public static string Exigir(string origen)
    {
        using var recurso = Assembly.GetExecutingAssembly().GetManifestResourceStream("Clarisa.Setup.HuellaPaquete")
            ?? throw new InvalidOperationException("Esta compilación no incluye un paquete de instalación verificado. Usa la distribución completa.");
        using var lector = new StreamReader(recurso);
        var esperada = lector.ReadToEnd().Trim();
        if (esperada.Length != 64 || !esperada.All(Uri.IsHexDigit))
            throw new InvalidOperationException("La identidad del paquete incluida en el instalador no es válida.");
        using var archivo = new FileStream(Path.Combine(origen, "manifiesto-instalacion.json"), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (archivo.Length > 8 * 1024 * 1024 || !Convert.ToHexString(SHA256.HashData(archivo)).Equals(esperada, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El paquete fue sustituido o no corresponde a este instalador. Vuelve a obtener la distribución completa.");
        return esperada;
    }
}
