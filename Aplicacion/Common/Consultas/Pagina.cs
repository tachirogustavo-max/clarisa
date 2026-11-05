namespace Clarisa.Application.Common.Consultas;

public sealed record Pagina<T>(IReadOnlyList<T> Elementos, int Numero, int Tamanio, int Total)
{
    public Pagina<TDestino> Convertir<TDestino>(Func<T, TDestino> conversion) =>
        new(Elementos.Select(conversion).ToList(), Numero, Tamanio, Total);
}
