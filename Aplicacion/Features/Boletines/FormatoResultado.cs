using System.Globalization;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Boletines;

// Único formateador de resultados: el servidor envía el texto ya formateado para que PDF, reporte y escritorio coincidan.
// Con decimales definidos por la política se muestran fijos (15,50); sin política, solo los significativos (15,5).
public static class FormatoResultado
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-VE");

    public const string SinVerificar = "Sin verificar";

    public static string Texto(EstadoResultadoBoletin estado, decimal? valor, string? letra, int? decimales, bool abreviado = false) =>
        estado switch
        {
            EstadoResultadoBoletin.Pendiente when valor is null && letra is null => abreviado ? "Pend." : "Pendiente",
            EstadoResultadoBoletin.Numerico when valor is { } numero && letra is null => Numero(numero, decimales),
            EstadoResultadoBoletin.Cualitativo when valor is null && letra is not null => letra,
            EstadoResultadoBoletin.NumericoYCualitativo when valor is { } numero && letra is not null => $"{Numero(numero, decimales)} ({letra})",
            EstadoResultadoBoletin.Exonerado when valor is null && letra is null => "EX",
            EstadoResultadoBoletin.NoCursada when valor is null && letra is null => "-",
            _ => SinVerificar
        };

    public static string Definitiva(EstadoDefinitiva estado, decimal? valor, int? decimales, bool abreviado = false) => estado switch
    {
        EstadoDefinitiva.Pendiente => abreviado ? "Pend." : "Pendiente",
        EstadoDefinitiva.Calculada when valor is { } numero => Numero(numero, decimales),
        EstadoDefinitiva.NoCursada => "-",
        EstadoDefinitiva.Exonerada => "EX",
        // Materia evaluada solo con letra: no hay definitiva numérica y no se inventa una.
        EstadoDefinitiva.NoNumerica => abreviado ? "" : "Sin definitiva numérica",
        _ => SinVerificar
    };

    public static string Numero(decimal valor, int? decimales) =>
        decimales is { } fijos ? valor.ToString("F" + fijos, Cultura) : valor.ToString("0.##", Cultura);

    // Reportes y clientes anteriores envían el estado como entero; un valor desconocido no se interpreta.
    public static EstadoResultadoBoletin Estado(int? tipo, decimal? valor) => tipo switch
    {
        null => valor is null ? EstadoResultadoBoletin.Pendiente : EstadoResultadoBoletin.Numerico,
        var conocido when Enum.IsDefined((EstadoResultadoBoletin)conocido) => (EstadoResultadoBoletin)conocido,
        _ => (EstadoResultadoBoletin)(-1)
    };
}
