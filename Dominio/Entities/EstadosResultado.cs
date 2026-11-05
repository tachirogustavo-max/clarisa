namespace Clarisa.Domain.Entities;

public enum TipoResultado
{
    Pendiente = 0,
    Numerico = 1,
    Cualitativo = 2,
    NumericoYCualitativo = 3,
    Exonerado = 4
}

public enum ModalidadEvaluacion
{
    NoCursada = 1,
    Numerica = 2,
    Cualitativa = 3,
    NumericaYCualitativa = 4
}

public static class ReglasModalidad
{
    // Sin plan configurado solo se admite lo que ya existía antes de los estados: nota numérica, pendiente y la exoneración administrativa.
    public static bool Admite(ModalidadEvaluacion? modalidad, TipoResultado tipo) => tipo switch
    {
        TipoResultado.Pendiente => true,
        TipoResultado.Exonerado => modalidad != ModalidadEvaluacion.NoCursada,
        TipoResultado.Numerico => modalidad is null or ModalidadEvaluacion.Numerica,
        TipoResultado.Cualitativo => modalidad == ModalidadEvaluacion.Cualitativa,
        TipoResultado.NumericoYCualitativo => modalidad == ModalidadEvaluacion.NumericaYCualitativa,
        _ => false
    };

    public static bool AdmiteCarga(ModalidadEvaluacion? modalidad) => modalidad != ModalidadEvaluacion.NoCursada;
}
