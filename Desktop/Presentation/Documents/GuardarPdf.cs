using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Clarisa.Desktop.Presentation.Documents;
public static class GuardarPdf
{
    public static async Task<string?> ElegirYGuardarAsync(Window? propietario, byte[] contenido, string nombre)
    {
        var dialogo = new SaveFileDialog
        {
            Filter = "Documento PDF (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            FileName = nombre,
            OverwritePrompt = true,
            CheckPathExists = true
        };
        if (dialogo.ShowDialog(propietario) != true)
            return null;
        try
        {
            await EscribirAsync(dialogo.FileName, contenido, CancellationToken.None);
            return "PDF guardado. Puedes abrirlo para imprimirlo.";
        }
        catch (IOException)
        {
            return "No se pudo guardar el PDF. Revisa el espacio disponible y si el archivo está abierto.";
        }
        catch (UnauthorizedAccessException)
        {
            return "No tienes permiso para guardar en esa carpeta. Elige otra ubicación.";
        }
    }

    public static async Task EscribirAsync(string destino, byte[] contenido, CancellationToken ct)
    {
        if (contenido.Length is < 20 or > Services.ClienteDocumentos.MaximoBytesSeccion || !contenido.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            throw new ArgumentException("El contenido no es un PDF válido para guardar.", nameof(contenido));
        var ruta = Path.GetFullPath(destino);
        var carpeta = Path.GetDirectoryName(ruta)!;
        var temporal = Path.Combine(carpeta, ".documento-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await File.WriteAllBytesAsync(temporal, contenido, ct);
            ct.ThrowIfCancellationRequested();
            File.Move(temporal, ruta, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporal))
                    File.Delete(temporal);
            }
            catch (Exception error)when (error is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.TraceWarning("No se pudo retirar un temporal de documento: {0}.", error.GetType().Name);
            }
        }
    }
}
