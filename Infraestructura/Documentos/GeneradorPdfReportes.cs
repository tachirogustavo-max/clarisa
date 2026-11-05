using System.Globalization;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Features.Reportes;
using Clarisa.Infrastructure.Configuration;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Drawing;

namespace Clarisa.Infrastructure.Documentos;

public sealed class GeneradorPdfReportes(ConfiguracionInformes configuracion) : IGeneradorPdfReportes
{
    private static readonly SemaphoreSlim turno = new(1, 1);
    private static readonly CultureInfo cultura = CultureInfo.GetCultureInfo("es-VE");
    private static readonly Color verde = Color.FromRgb(20, 58, 43);
    public async Task<byte[]> GenerarAsync(DocumentoReporte documento, CancellationToken ct)
    {
        if (documento.Filas.Count > ExportacionReportes.MaximoFilas) throw Error.EstadoNoPermitido("Reduce los filtros del informe antes de exportar.");
        await turno.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            RecursosInforme.Preparar();
            var contenido = CrearDocumento(documento);
            var motor = new PdfDocumentRenderer { Document = contenido };
            motor.RenderDocument();
            motor.PdfDocument.Info.CreationDate = documento.ConsultadoEn.UtcDateTime;
            ct.ThrowIfCancellationRequested();
            using var salida = new MemoryStream();
            motor.PdfDocument.Save(salida, closeStream: false);
            if (salida.Length > 10 * 1024 * 1024) throw Error.EstadoNoPermitido("El informe es demasiado grande. Utiliza filtros más específicos.");
            return salida.ToArray();
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or IOException)
        {
            throw Error.EstadoNoPermitido("No se pudo preparar el documento. Revisa la configuración de informes del servidor.");
        }
        finally { turno.Release(); }
    }
    private Document CrearDocumento(DocumentoReporte datos)
    {
        var documento = new Document();
        documento.Info.Title = "Calificaciones registradas";
        documento.Info.Author = configuracion.Institucion;
        var normal = documento.Styles[StyleNames.Normal]!;
        normal.Font.Name = "InformeSans";
        normal.Font.Size = Unit.FromPoint(9);
        normal.Font.Color = verde;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(4);
        var seccion = documento.AddSection();
        seccion.PageSetup.PageFormat = PageFormat.Letter;
        seccion.PageSetup.Orientation = Orientation.Landscape;
        seccion.PageSetup.TopMargin = Unit.FromPoint(configuracion.Institucion.Length > 80 || configuracion.Localidad.Length > 60 ? 142 : 112);
        seccion.PageSetup.BottomMargin = Unit.FromPoint(38);
        seccion.PageSetup.LeftMargin = seccion.PageSetup.RightMargin = Unit.FromPoint(36);
        seccion.PageSetup.HeaderDistance = Unit.FromPoint(16);
        seccion.PageSetup.FooterDistance = Unit.FromPoint(16);
        MembreteInstitucional.Agregar(seccion, configuracion, 720);
        var titulo = seccion.AddParagraph("Calificaciones registradas");
        titulo.Format.Font.Name = "InformeSerif";
        titulo.Format.Font.Size = Unit.FromPoint(23);
        titulo.Format.SpaceAfter = Unit.FromPoint(5);
        var fecha = TimeZoneInfo.ConvertTime(datos.ConsultadoEn, TimeZoneInfo.FindSystemTimeZoneById(configuracion.ZonaHoraria));
        var detalle = seccion.AddParagraph($"Consulta: {fecha.ToString("dd/MM/yyyy HH:mm", cultura)}   |   Registros: {datos.Filas.Count}");
        detalle.Format.Font.Size = Unit.FromPoint(9);
        detalle.Format.SpaceAfter = Unit.FromPoint(12);
        if (!string.IsNullOrEmpty(datos.Busqueda)) seccion.AddParagraph("Búsqueda: " + datos.Busqueda);
        Tabla(seccion, datos.Filas);
        var pie = seccion.Footers.Primary.AddParagraph();
        pie.Format.Font.Size = Unit.FromPoint(8);
        pie.Format.Alignment = ParagraphAlignment.Right;
        pie.AddText(configuracion.Localidad + "   |   Página ");
        pie.AddPageField();
        pie.AddText(" de ");
        pie.AddNumPagesField();
        return documento;
    }
    private static void Tabla(Section seccion, IReadOnlyList<FilaReporte> datos)
    {
        if (datos.Count == 0) { seccion.AddParagraph("No hay calificaciones registradas para los filtros seleccionados."); return; }
        var tabla = seccion.AddTable();
        using var medida = XGraphics.CreateMeasureContext(new XSize(1000, 1000), XGraphicsUnit.Point, XPageDirection.Downwards);
        var regular = new XFont("InformeSans", 8.5, XFontStyleEx.Regular);
        var negrita = new XFont("InformeSans", 8.5, XFontStyleEx.Bold);
        double[] anchos = [65, 125, 65, 105, 80, 60, 65, 155];
        foreach (var ancho in anchos) tabla.AddColumn(Unit.FromPoint(ancho));
        tabla.Format.Font.Size = Unit.FromPoint(8.5);
        tabla.Rows.LeftIndent = 0;
        tabla.TopPadding = tabla.BottomPadding = Unit.FromPoint(5);
        tabla.LeftPadding = tabla.RightPadding = Unit.FromPoint(5);
        var cabecera = tabla.AddRow();
        cabecera.HeadingFormat = true;
        cabecera.Shading.Color = verde;
        cabecera.Format.Font.Color = Colors.White;
        cabecera.Format.Font.Bold = true;
        string[] titulos = ["Año escolar", "Estudiante", "Cédula", "Materia", "Sección", "Momento", "Nota", "Docente"];
        for (var indice = 0; indice < titulos.Length; indice++) cabecera.Cells[indice].AddParagraph(titulos[indice]);
        var alterna = false;
        foreach (var dato in datos)
        {
            var fila = tabla.AddRow();
            fila.VerticalAlignment = VerticalAlignment.Center;
            if (alterna) fila.Shading.Color = Color.FromRgb(243, 247, 242);
            alterna = !alterna;
            string[] valores = [dato.AnioLectivo, dato.Estudiante, dato.Cedula ?? "Sin cédula", dato.Materia, dato.Seccion, dato.Lapso,
                dato.Resultado, dato.Docente];
            for (var indice = 0; indice < valores.Length; indice++) TextoEnCeldas.Escribir(fila.Cells[indice], valores[indice], anchos[indice] - 10,
                medida, indice == 6 ? negrita : regular);
            fila.Cells[6].Format.Font.Bold = true;
            fila.Cells[6].Format.Alignment = ParagraphAlignment.Center;
            fila.Borders.Bottom.Width = Unit.FromPoint(.3);
            fila.Borders.Bottom.Color = Color.FromRgb(218, 229, 217);
        }
    }
}
