using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Clarisa.Desktop.Presentation.Documents;

// Convierte el PDF que generó el servidor en imágenes para la vista previa, así lo que se ve es exactamente el documento que se
// guarda o se imprime. El dibujo lo hace este mismo programa en un proceso aparte (ver DibujoPdf) y termina en segundos.
public static class PaginasPdf
{
    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(30);

    public static async Task<IReadOnlyList<ImageSource>> DibujarAsync(byte[] contenido, CancellationToken ct)
    {
        var programa = Path.Combine(AppContext.BaseDirectory, "Clarisa.Desktop.exe");
        if (!File.Exists(programa)) return [];
        var carpeta = Path.Combine(Path.GetTempPath(), "Clarisa", "vista-previa", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        try
        {
            var archivo = Path.Combine(carpeta, "documento.pdf");
            await File.WriteAllBytesAsync(archivo, contenido, ct);
            var inicio = new ProcessStartInfo(programa) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            foreach (var argumento in new[] { DibujoPdf.Opcion, archivo, carpeta }) inicio.ArgumentList.Add(argumento);
            using var proceso = Process.Start(inicio) ?? throw new InvalidOperationException("No se pudo preparar la vista previa.");
            using var espera = CancellationTokenSource.CreateLinkedTokenSource(ct);
            espera.CancelAfter(Limite);
            try
            {
                await proceso.WaitForExitAsync(espera.Token);
            }
            catch (OperationCanceledException)
            {
                proceso.Kill();
                throw;
            }
            if (proceso.ExitCode != 0) return [];
            var paginas = new List<ImageSource>();
            for (var numero = 1; numero <= DibujoPdf.MaximoPaginas; numero++)
            {
                var imagen = Path.Combine(carpeta, $"pagina-{numero}.png");
                if (!File.Exists(imagen)) break;
                paginas.Add(Cargar(await File.ReadAllBytesAsync(imagen, ct)));
            }
            return paginas;
        }
        finally
        {
            try
            {
                Directory.Delete(carpeta, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning("No se pudo retirar una vista previa temporal: {0}.", error.GetType().Name);
            }
        }
    }

    private static ImageSource Cargar(byte[] png)
    {
        using var flujo = new MemoryStream(png, writable: false);
        var imagen = new BitmapImage();
        imagen.BeginInit();
        imagen.CacheOption = BitmapCacheOption.OnLoad;
        imagen.StreamSource = flujo;
        imagen.EndInit();
        imagen.Freeze();
        return imagen;
    }
}
