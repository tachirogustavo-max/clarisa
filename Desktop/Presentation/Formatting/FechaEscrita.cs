using System.Globalization;

namespace Clarisa.Desktop.Presentation.Formatting;

public static class FechaEscrita
{
    public static bool Intentar(string? texto, out DateTime? fecha)
    {
        fecha = null;
        if (string.IsNullOrWhiteSpace(texto)) return true;
        if (!DateTime.TryParseExact(texto.Trim(), ["d/M/yyyy", "dd/MM/yyyy", "d/MM/yyyy", "dd/M/yyyy"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var valor)) return false;
        fecha = valor.Date;
        return true;
    }

    public static string Formatear(DateTime? fecha) => fecha?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "";
}
