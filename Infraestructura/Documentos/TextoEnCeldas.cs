using System.Globalization;
using System.Text.RegularExpressions;
using MigraDoc.DocumentObjectModel.Tables;
using PdfSharp.Drawing;

namespace Clarisa.Infrastructure.Documentos;

internal static class TextoEnCeldas
{
    internal static void Escribir(Cell celda, string texto, double ancho, XGraphics medida, XFont fuente)
    {
        var parrafo = celda.AddParagraph();
        foreach (var palabra in Regex.Split(texto, "(\\s+)"))
        {
            if (medida.MeasureString(palabra, fuente).Width <= ancho) { parrafo.AddText(palabra); continue; }
            var fragmento = "";
            var caracteres = StringInfo.GetTextElementEnumerator(palabra);
            while (caracteres.MoveNext())
            {
                var siguiente = caracteres.GetTextElement();
                if (fragmento.Length > 0 && medida.MeasureString(fragmento + siguiente, fuente).Width > ancho)
                {
                    parrafo.AddText(fragmento);
                    parrafo.AddLineBreak();
                    fragmento = "";
                }
                fragmento += siguiente;
            }
            parrafo.AddText(fragmento);
        }
    }
}
