using Clarisa.Infrastructure.Configuration;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using PdfSharp.Drawing;

namespace Clarisa.Infrastructure.Documentos;

internal static class MembreteInstitucional
{
    internal static void Agregar(Section seccion, ConfiguracionInformes configuracion, double ancho)
    {
        var tabla = seccion.Headers.Primary.AddTable();
        tabla.AddColumn(Unit.FromPoint(116));
        tabla.AddColumn(Unit.FromPoint(ancho - 232));
        tabla.AddColumn(Unit.FromPoint(116));
        tabla.LeftPadding = tabla.RightPadding = Unit.FromPoint(2);
        var fila = tabla.AddRow();
        fila.VerticalAlignment = VerticalAlignment.Center;
        fila.Cells[0].Format.Alignment = fila.Cells[2].Format.Alignment = ParagraphAlignment.Center;
        AgregarLogo(fila.Cells[0], "logo-ministerio.png", 110);
        AgregarLogo(fila.Cells[2], "logo-liceo.png", 110);

        var centro = fila.Cells[1];
        centro.Format.Alignment = ParagraphAlignment.Center;
        using var medida = XGraphics.CreateMeasureContext(new XSize(1000, 1000), XGraphicsUnit.Point, XPageDirection.Downwards);
        var fuente = new XFont("InformeSans", 8, XFontStyleEx.Bold);
        foreach (var texto in new[] { configuracion.Republica, configuracion.Ministerio, configuracion.Institucion,
                     configuracion.Localidad, configuracion.Coordinacion })
        {
            TextoEnCeldas.Escribir(centro, texto, ancho - 240, medida, fuente);
            var parrafo = centro.Elements.OfType<Paragraph>().Last();
            parrafo.Format.Font.Name = "InformeSans";
            parrafo.Format.Font.Size = Unit.FromPoint(8);
            parrafo.Format.Font.Bold = true;
            parrafo.Format.Font.Color = Colors.Black;
            parrafo.Format.SpaceAfter = Unit.FromPoint(1);
            parrafo.Format.SpaceBefore = 0;
        }
    }

    private static void AgregarLogo(Cell celda, string recurso, double ancho)
    {
        var imagen = celda.AddImage("base64:" + Convert.ToBase64String(RecursosInforme.Leer(recurso)));
        imagen.Width = Unit.FromPoint(ancho);
        imagen.LockAspectRatio = true;
    }
}
