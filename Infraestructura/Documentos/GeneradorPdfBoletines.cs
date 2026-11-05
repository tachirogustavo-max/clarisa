using Clarisa.Application.Common.Errores;
using Clarisa.Application.Features.Boletines;
using Clarisa.Infrastructure.Configuration;
using MigraDoc.Rendering;

namespace Clarisa.Infrastructure.Documentos;

public sealed class GeneradorPdfBoletines(ConfiguracionInformes configuracion) : IGeneradorPdfBoletines
{
    private static readonly SemaphoreSlim turno = new(1, 1);

    public const int MaximoBoletines = 80;
    public const int MaximoBytes = 40 * 1024 * 1024;

    public async Task<byte[]> GenerarAsync(IReadOnlyList<DocumentoBoletin> documentos, CancellationToken ct)
    {
        if (documentos.Count is 0 or > MaximoBoletines) throw Error.EstadoNoPermitido($"Se generan entre 1 y {MaximoBoletines} boletines por documento.");
        if (documentos.Any(d => d.Boletin.Materias.Count > 200)) throw Error.EstadoNoPermitido("El boletín supera el límite de materias admitido.");
        await turno.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            configuracion.Validar();
            RecursosInforme.Preparar();
            var motor = new PdfDocumentRenderer { Document = new PlantillaBoletin(configuracion).Crear(documentos) };
            motor.RenderDocument();
            motor.PdfDocument.Info.CreationDate = documentos[0].ConsultadoEn.UtcDateTime;
            ct.ThrowIfCancellationRequested();
            using var salida = new MemoryStream();
            motor.PdfDocument.Save(salida, closeStream: false);
            if (salida.Length > MaximoBytes) throw Error.EstadoNoPermitido("No se pudo preparar un boletín de tamaño admisible.");
            return salida.ToArray();
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or IOException)
        { throw Error.EstadoNoPermitido("No se pudo preparar el boletín. Revisa los recursos de impresión del servidor."); }
        finally { turno.Release(); }
    }
}
