using Clarisa.Application.Common.Errores;

namespace Clarisa.Application.Common.Consultas;

public sealed record ConsultaPagina(int Numero, int Tamanio)
{
    public const int NumeroMaximo = 100_000;
    public const int TamanioMaximo = 200;

    public int Desplazamiento => (Numero - 1) * Tamanio;

    public static ConsultaPagina Validar(ConsultaPagina? consulta)
    {
        var valida = Error.SolicitudRequerida(consulta);
        if (valida.Numero is < 1 or > NumeroMaximo)
            throw Error.DatosInvalidos($"La página debe estar entre 1 y {NumeroMaximo}.", nameof(Numero));
        if (valida.Tamanio is < 1 or > TamanioMaximo)
            throw Error.DatosInvalidos($"El tamaño de página debe estar entre 1 y {TamanioMaximo}.", nameof(Tamanio));
        return valida;
    }
}
