using Clarisa.Application.Features.Firmantes;
using Clarisa.Infrastructure.Configuration;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace Clarisa.Infrastructure.Documentos;

internal sealed class CierreBoletin(ConfiguracionInformes configuracion, NombresFirmantes firmantes)
{
    internal void Agregar(Section seccion)
    {
        var tabla = seccion.AddTable();
        foreach (var ancho in new double[] { 146, 104, 224 }) tabla.AddColumn(Unit.FromPoint(ancho));
        tabla.LeftPadding = tabla.RightPadding = Unit.FromPoint(2);
        tabla.TopPadding = tabla.BottomPadding = 0;
        tabla.Borders.Width = Unit.FromPoint(.7);
        tabla.Borders.Color = Colors.Black;
        tabla.Rows.Alignment = RowAlignment.Center;
        Observacion(tabla, configuracion.PrimeraObservacionBoletin, 27);
        Observacion(tabla, configuracion.SegundaObservacionBoletin, 42);
        var director = tabla.AddRow();
        director.Height = Unit.FromPoint(43);
        director.KeepWith = 5;
        director.Cells[0].MergeDown = director.Cells[1].MergeDown = 2;
        var nombreDirector = tabla.AddRow();
        nombreDirector.Height = Unit.FromPoint(14);
        Nombre(nombreDirector.Cells[2], firmantes.Director, 220);
        var rolDirector = tabla.AddRow();
        rolDirector.Height = Unit.FromPoint(14);
        Rol(rolDirector.Cells[2], "DIRECTOR");
        var firmas = tabla.AddRow();
        firmas.Height = Unit.FromPoint(28);
        firmas.Cells[0].MergeRight = 1;
        var nombres = tabla.AddRow();
        nombres.Height = Unit.FromPoint(14);
        nombres.Cells[0].MergeRight = 1;
        Nombre(nombres.Cells[0], firmantes.CoordinadorEvaluacion, 246);
        Nombre(nombres.Cells[2], firmantes.CoordinadorEstudios, 220);
        var roles = tabla.AddRow();
        roles.Height = Unit.FromPoint(14);
        roles.Cells[0].MergeRight = 1;
        Rol(roles.Cells[0], "COORD. DE EVALUACIÓN");
        Rol(roles.Cells[2], "COORD. CONTROL DE ESTUDIO");
    }

    private static void Observacion(Table tabla, string? texto, double alto)
    {
        var fila = tabla.AddRow();
        fila.Height = Unit.FromPoint(alto);
        fila.Cells[0].MergeRight = 2;
        if (!string.IsNullOrWhiteSpace(texto)) PlantillaBoletin.Texto(fila.Cells[0], texto, 470, 10);
    }

    private static void Nombre(Cell celda, string? nombre, double ancho)
    {
        PlantillaBoletin.Texto(celda, (nombre ?? "").ToUpperInvariant(), ancho, 10);
        celda.Format.Alignment = ParagraphAlignment.Center;
        celda.VerticalAlignment = VerticalAlignment.Center;
    }

    private static void Rol(Cell celda, string rol)
    {
        celda.AddParagraph(rol);
        celda.Format.Alignment = ParagraphAlignment.Center;
        celda.VerticalAlignment = VerticalAlignment.Center;
    }
}
