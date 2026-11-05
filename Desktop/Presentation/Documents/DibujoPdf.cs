using System.IO;
using Windows.Data.Pdf;
using Windows.Storage.Streams;

namespace Clarisa.Desktop.Presentation.Documents;

// Modo de proceso aparte: lee un PDF y escribe sus primeras páginas como pagina-1.png, pagina-2.png... en la carpeta indicada.
// Solo lo invoca PaginasPdf, con archivos en una carpeta temporal propia.
public static class DibujoPdf
{
    public const string Opcion = "--dibujar-pdf";
    public const int MaximoPaginas = 4;
    public const uint AnchoPixeles = 1240;

    public static int Ejecutar(string archivoPdf, string carpeta)
    {
        var codigo = 1;
        var hilo = new Thread(() =>
        {
            try
            {
                DibujarAsync(archivoPdf, carpeta).GetAwaiter().GetResult();
                codigo = 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error.GetType().Name);
            }
        });
        hilo.SetApartmentState(ApartmentState.MTA);
        hilo.Start();
        hilo.Join();
        return codigo;
    }

    private static async Task DibujarAsync(string archivoPdf, string carpeta)
    {
        var entrada = new InMemoryRandomAccessStream();
        using (var escritor = new DataWriter(entrada.GetOutputStreamAt(0)))
        {
            escritor.WriteBytes(await File.ReadAllBytesAsync(archivoPdf));
            await escritor.StoreAsync();
            await escritor.FlushAsync();
            escritor.DetachStream();
        }
        var documento = await PdfDocument.LoadFromStreamAsync(entrada);
        for (uint indice = 0; indice < Math.Min(documento.PageCount, MaximoPaginas); indice++)
        {
            using var pagina = documento.GetPage(indice);
            var salida = new InMemoryRandomAccessStream();
            await pagina.RenderToStreamAsync(salida, new PdfPageRenderOptions { DestinationWidth = AnchoPixeles });
            var bytes = new byte[salida.Size];
            using (var lector = new DataReader(salida.GetInputStreamAt(0)))
            {
                await lector.LoadAsync((uint)salida.Size);
                lector.ReadBytes(bytes);
                lector.DetachStream();
            }
            await File.WriteAllBytesAsync(Path.Combine(carpeta, $"pagina-{indice + 1}.png"), bytes);
        }
    }
}
