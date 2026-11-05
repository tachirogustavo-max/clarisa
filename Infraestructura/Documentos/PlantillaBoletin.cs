using Clarisa.Application.Features.Boletines;
using Clarisa.Domain.Entities;
using Clarisa.Infrastructure.Configuration;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using PdfSharp.Drawing;

namespace Clarisa.Infrastructure.Documentos;

internal sealed class PlantillaBoletin(ConfiguracionInformes configuracion)
{
    internal const double Ancho = 474;

    internal Document Crear(IReadOnlyList<DocumentoBoletin> boletines)
    {
        var documento = new Document();
        documento.Info.Title = "Boletín informativo";
        documento.Info.Author = configuracion.Institucion;
        var normal = documento.Styles[StyleNames.Normal]!;
        normal.Font.Name = "BoletinSerif";
        normal.Font.Size = 10;
        normal.Font.Color = Colors.Black;
        normal.ParagraphFormat.SpaceAfter = 0;
        // Cada boletín es una sección del documento: empieza en página nueva y lleva su propia cabecera.
        foreach (var datos in boletines) Agregar(documento, datos);
        return documento;
    }

    private void Agregar(Document documento, DocumentoBoletin datos)
    {
        var seccion = documento.AddSection();
        seccion.PageSetup.PageFormat = PageFormat.Letter;
        seccion.PageSetup.TopMargin = Unit.FromPoint(AlturaInicio());
        seccion.PageSetup.LeftMargin = seccion.PageSetup.RightMargin = Unit.FromPoint(69);
        seccion.PageSetup.BottomMargin = Unit.FromPoint(38);
        seccion.PageSetup.HeaderDistance = Unit.FromPoint(72);
        Cabecera(seccion);
        Identidad(seccion, datos.Boletin);
        TablaCalificacionesBoletin.Agregar(seccion, datos.Boletin);
        new CierreBoletin(configuracion, datos.Firmantes).Agregar(seccion);
    }

    private void Cabecera(Section seccion)
    {
        var tabla = seccion.Headers.Primary.AddTable();
        foreach (var ancho in new double[] { 126, 222, 126 }) tabla.AddColumn(Unit.FromPoint(ancho));
        tabla.LeftPadding = tabla.RightPadding = 0;
        tabla.Rows.Alignment = RowAlignment.Center;
        var fila = tabla.AddRow();
        fila.VerticalAlignment = VerticalAlignment.Center;
        Logo(fila.Cells[0], "logo-ministerio.png", 118);
        Logo(fila.Cells[2], "logo-liceo.png", 118);
        fila.Cells[0].Format.Alignment = fila.Cells[2].Format.Alignment = ParagraphAlignment.Center;
        foreach (var texto in LineasMembrete())
        {
            Texto(fila.Cells[1], texto.ToUpperInvariant(), 218, 8.2, true, "InformeSans");
            fila.Cells[1].Format.Alignment = ParagraphAlignment.Center;
        }
    }

    private string[] LineasMembrete() => [configuracion.Republica, configuracion.MinisterioBoletin,
        configuracion.InstitucionBoletin, configuracion.Localidad, configuracion.CoordinacionBoletin];

    private double AlturaInicio()
    {
        using var medida = XGraphics.CreateMeasureContext(new XSize(1000, 1000), XGraphicsUnit.Point, XPageDirection.Downwards);
        var fuente = new XFont("InformeSans", 8.2, XFontStyleEx.Bold);
        var lineas = 0;
        foreach (var texto in LineasMembrete())
        {
            var anchoLinea = 0d;
            lineas++;
            foreach (var palabra in texto.ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var ancho = medida.MeasureString(palabra + " ", fuente).Width;
                if (anchoLinea > 0 && anchoLinea + ancho > 214) { lineas++; anchoLinea = 0; }
                while (ancho > 214) { lineas++; ancho -= 214; }
                anchoLinea += ancho;
            }
        }
        return Math.Max(160, 72 + lineas * 12 + 18);
    }

    private void Identidad(Section seccion, BoletinDatos boletin)
    {
        Centrado(seccion, "BOLETÍN INFORMATIVO", 12);
        Centrado(seccion, "AÑO ESCOLAR " + boletin.AnioLectivo, 11);
        string[] momentos = ["I", "II", "III"];
        var momento = boletin.Lapsos.Count == 1 && boletin.Lapsos[0].Orden is >= 1 and <= 3
            ? momentos[boletin.Lapsos[0].Orden - 1] + " MOMENTO" : "I, II Y III MOMENTO";
        Centrado(seccion, momento, 10.5);
        if (boletin.EstadoInscripcion != EstadoInscripcion.Activa)
            Centrado(seccion, "INSCRIPCIÓN RETIRADA", 10);
        var titulo = seccion.AddParagraph("DATOS DE ESTUDIANTE (A):");
        titulo.Format.Font.Bold = true;
        titulo.Format.Alignment = ParagraphAlignment.Center;
        titulo.Format.SpaceBefore = Unit.FromPoint(22);
        var identidad = seccion.AddTable();
        identidad.AddColumn(Unit.FromPoint(Ancho));
        identidad.LeftPadding = identidad.RightPadding = 0;
        Texto(identidad.AddRow().Cells[0], "APELLIDOS Y NOMBRES: " + boletin.Apellidos.ToUpperInvariant()
            + " " + boletin.Nombres.ToUpperInvariant(), Ancho, 10, true);
        var detalle = seccion.AddTable();
        foreach (var ancho in new double[] { 286, 72, 116 }) detalle.AddColumn(Unit.FromPoint(ancho));
        detalle.LeftPadding = detalle.RightPadding = 0;
        var fila = detalle.AddRow();
        Texto(fila.Cells[0], "C.I N°: " + (boletin.Identificacion ?? ""), 282, 10, true);
        Texto(fila.Cells[1], "AÑO: " + boletin.Grado.ToUpperInvariant(), 68, 10, true);
        Texto(fila.Cells[2], "SECCIÓN: " + boletin.Seccion.ToUpperInvariant(), 112, 10, true);
        var orientador = seccion.AddParagraph("ORIENTADOR(A): " + (configuracion.Orientador ?? "").ToUpperInvariant());
        orientador.Format.Font.Bold = true;
        orientador.Format.SpaceBefore = Unit.FromPoint(23);
        orientador.Format.SpaceAfter = Unit.FromPoint(15);
    }

    private static void Centrado(Section seccion, string texto, double puntos)
    {
        var parrafo = seccion.AddParagraph(texto);
        parrafo.Format.Font.Size = Unit.FromPoint(puntos);
        parrafo.Format.Font.Bold = true;
        parrafo.Format.Alignment = ParagraphAlignment.Center;
    }

    private static void Logo(Cell celda, string nombre, double ancho)
    {
        var parrafo = celda.AddParagraph();
        parrafo.Format.Alignment = ParagraphAlignment.Center;
        var imagen = parrafo.AddImage("base64:" + Convert.ToBase64String(RecursosInforme.Leer(nombre)));
        imagen.Width = Unit.FromPoint(ancho);
        imagen.LockAspectRatio = true;
    }

    internal static void Texto(Cell celda, string texto, double ancho, double puntos, bool negrita = false,
        string familia = "BoletinSerif")
    {
        using var medida = XGraphics.CreateMeasureContext(new XSize(1000, 1000), XGraphicsUnit.Point, XPageDirection.Downwards);
        TextoEnCeldas.Escribir(celda, texto, ancho, medida,
            new XFont(familia, puntos, negrita ? XFontStyleEx.Bold : XFontStyleEx.Regular));
        var parrafo = celda.Elements.OfType<Paragraph>().Last();
        parrafo.Format.Font.Name = familia;
        parrafo.Format.Font.Size = Unit.FromPoint(puntos);
        parrafo.Format.Font.Bold = negrita;
    }
}
