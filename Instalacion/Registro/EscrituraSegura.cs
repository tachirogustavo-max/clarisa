namespace Clarisa.Instalacion.Registro;

// El temporal tiene nombre impredecible y se crea nuevo: un archivo plantado con el nombre esperado no puede aportar su ACL ni
// su propietario al resultado. Se vuelca al disco antes de sustituir para sobrevivir a un corte eléctrico.
internal static class EscrituraSegura
{
    public static void Reemplazar(string ruta, Action<Stream> escribir)
    {
        RutasSeguras.ExigirSinPuntosReparacion(ruta);
        var temporal = $"{ruta}.{Guid.NewGuid():N}.nuevo";
        try
        {
            using (var flujo = new FileStream(temporal, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                escribir(flujo);
                flujo.Flush(flushToDisk: true);
            }
            File.Move(temporal, ruta, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporal)) File.Delete(temporal);
        }
    }

    public static void ReemplazarTexto(string ruta, string contenido) =>
        Reemplazar(ruta, flujo => flujo.Write(System.Text.Encoding.UTF8.GetBytes(contenido)));
}
