namespace Clarisa.Desktop.Services;

public static class ModalidadRemota
{
    public const int NoCursada = 1;
    public const int Numerica = 2;
    public const int Cualitativa = 3;
    public const int NumericaYCualitativa = 4;

    public static bool AdmiteNumero(int modalidad) => modalidad is Numerica or NumericaYCualitativa;
    public static bool AdmiteLetra(int modalidad) => modalidad is Cualitativa or NumericaYCualitativa;
}

public static class TipoResultadoRemoto
{
    public const int Pendiente = 0;
    public const int Numerico = 1;
    public const int Cualitativo = 2;
    public const int NumericoYCualitativo = 3;
    public const int Exonerado = 4;
    public const int NoCursada = 5;
}

// La escala llega en la planilla y en la política del boletín; estos límites solo aplican si un contrato no la trae (reportes).
public static class EscalaRemota
{
    public const decimal Minima = 1m;
    public const decimal Maxima = 20m;

    public static bool Valida(decimal minima, decimal maxima) => minima >= Minima && maxima <= Maxima && minima < maxima;
}
