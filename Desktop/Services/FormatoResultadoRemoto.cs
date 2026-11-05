namespace Clarisa.Desktop.Services;

// Solo comprueba coherencia del dato recibido; el texto visible lo formatea el servidor para que PDF, reporte y escritorio coincidan.
public static class FormatoResultadoRemoto
{
    public const string SinVerificar = "Sin verificar";

    public static bool Coherente(int estado, decimal? valor, string? letra, decimal minima = EscalaRemota.Minima, decimal maxima = EscalaRemota.Maxima)
    {
        if (valor is { } numero && (numero < minima || numero > maxima || decimal.Round(numero, 2) != numero)) return false;
        return estado switch
        {
            TipoResultadoRemoto.Pendiente or TipoResultadoRemoto.Exonerado or TipoResultadoRemoto.NoCursada => valor is null && letra is null,
            TipoResultadoRemoto.Numerico => valor is not null && letra is null,
            TipoResultadoRemoto.Cualitativo => valor is null && ValidacionPlanillaRemota.LetraValida(letra),
            TipoResultadoRemoto.NumericoYCualitativo => valor is not null && ValidacionPlanillaRemota.LetraValida(letra),
            _ => false
        };
    }
}
