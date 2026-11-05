using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Clarisa.Desktop.Presentation.Documents;

// Guarda el PDF en una carpeta temporal propia y lo abre con el lector de PDF del equipo (en Windows 10 y 11, Microsoft Edge,
// que funciona sin Internet). Allí la persona elige la impresora, el papel y el número de copias, como con cualquier documento.
public static class AbrirParaImprimir
{
    private static readonly TimeSpan Conservar = TimeSpan.FromDays(1);

    public static async Task<string> AbrirAsync(byte[] contenido, string nombre)
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "Clarisa", "imprimir");
        try
        {
            Directory.CreateDirectory(carpeta);
            LimpiarAntiguos(carpeta);
            var ruta = Path.Combine(carpeta, nombre + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".pdf");
            await GuardarPdf.EscribirAsync(ruta, contenido, CancellationToken.None);
            Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true })?.Dispose();
            return "Se abrió el documento. Para imprimirlo, pulsa Imprimir en esa ventana (o Ctrl+P) y elige la impresora.";
        }
        catch (Win32Exception)
        {
            return "Este equipo no tiene un programa para abrir PDF. Usa Guardar PDF y ábrelo desde otro equipo para imprimir.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return "No se pudo preparar el documento para imprimir. Usa Guardar PDF y ábrelo desde la carpeta que elijas.";
        }
    }

    // Las copias de días anteriores ya se imprimieron o se descartaron; un archivo abierto en el lector se deja para la próxima vez.
    private static void LimpiarAntiguos(string carpeta)
    {
        foreach (var archivo in Directory.EnumerateFiles(carpeta, "*.pdf"))
        {
            try
            {
                if (DateTime.Now - File.GetLastWriteTime(archivo) > Conservar) File.Delete(archivo);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning("No se pudo retirar una copia para imprimir: {0}.", error.GetType().Name);
            }
        }
    }
}
