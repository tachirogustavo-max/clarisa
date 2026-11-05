using Clarisa.Application.Features.Boletines;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace Clarisa.Infrastructure.Documentos;

internal static class TablaCalificacionesBoletin
{
    internal static void Agregar(Section seccion, BoletinDatos boletin)
    {
        var tabla = seccion.AddTable();
        double[] anchos = [146, 52, 52, 52, 48, 48, 76];
        foreach (var ancho in anchos) tabla.AddColumn(Unit.FromPoint(ancho));
        tabla.Format.Font.Size = Unit.FromPoint(9);
        tabla.Format.Font.Bold = true;
        tabla.LeftPadding = tabla.RightPadding = Unit.FromPoint(2);
        tabla.TopPadding = tabla.BottomPadding = 0;
        tabla.Borders.Width = Unit.FromPoint(.7);
        tabla.Borders.Color = Colors.Black;
        tabla.Rows.Alignment = RowAlignment.Center;
        var cabecera = tabla.AddRow();
        cabecera.HeadingFormat = true;
        cabecera.Height = Unit.FromPoint(15);
        cabecera.VerticalAlignment = VerticalAlignment.Center;
        cabecera.Format.Alignment = ParagraphAlignment.Center;
        cabecera.Format.Font.Size = Unit.FromPoint(8.5);
        string[] titulos = ["ÁREA DE FORMACIÓN", "I MOM", "II MOM", "III MOM.", "DEFIN.", "REV.", "INASIST."];
        for (var i = 0; i < titulos.Length; i++) cabecera.Cells[i].AddParagraph(titulos[i]);
        foreach (var materia in boletin.Materias)
        {
            var fila = tabla.AddRow();
            fila.Height = Unit.FromPoint(14.5);
            fila.VerticalAlignment = VerticalAlignment.Center;
            PlantillaBoletin.Texto(fila.Cells[0], materia.Nombre.ToUpperInvariant(), anchos[0] - 4, 9, true);
            for (var orden = 1; orden <= 3; orden++)
            {
                var nota = materia.Notas.SingleOrDefault(n => n.Orden == orden);
                PlantillaBoletin.Texto(fila.Cells[orden], nota?.Texto ?? "", anchos[orden] - 4, 10, true);
                fila.Cells[orden].Format.Alignment = ParagraphAlignment.Center;
            }
            // La definitiva solo se imprime desde un cierre vigente; con datos provisionales la celda queda vacía.
            if (boletin.Cierre is not null && materia.Definitiva is { } definitiva)
            {
                PlantillaBoletin.Texto(fila.Cells[4], definitiva.Texto, anchos[4] - 4, 10, true);
                fila.Cells[4].Format.Alignment = ParagraphAlignment.Center;
            }
            Centrado(fila.Cells[6], Faltas(PromediosBoletin.Sumar(materia.Notas.Select(n => n.Inasistencias))), anchos[6]);
        }
        if (boletin.Materias.Count == 0)
        {
            var vacia = tabla.AddRow();
            vacia.Cells[0].MergeRight = 6;
            vacia.Cells[0].AddParagraph("Sin áreas de formación registradas.");
        }
        var promedio = tabla.AddRow();
        promedio.Height = Unit.FromPoint(14.5);
        promedio.VerticalAlignment = VerticalAlignment.Center;
        promedio.Cells[0].AddParagraph("PROMEDIO");
        foreach (var fila in boletin.Promedios)
        {
            // Cada momento va en su columna; el de las definitivas (sin momento) en DEFIN.
            var columna = fila.Orden ?? 4;
            Centrado(promedio.Cells[columna], fila.Texto, anchos[columna]);
        }
        Centrado(promedio.Cells[6], Faltas(PromediosBoletin.Sumar(boletin.Promedios.Where(p => p.LapsoId is not null)
            .Select(p => p.Inasistencias))), anchos[6]);
    }

    private static string Faltas(int? total) => total?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";

    private static void Centrado(Cell celda, string texto, double ancho)
    {
        PlantillaBoletin.Texto(celda, texto, ancho - 4, 10, true);
        celda.Format.Alignment = ParagraphAlignment.Center;
    }
}
