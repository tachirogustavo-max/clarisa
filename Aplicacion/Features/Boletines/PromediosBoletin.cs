using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Boletines;

// Arma la fila PROMEDIO: el promedio de cada momento con sus inasistencias totales y, si los resultados están cerrados, el de
// las definitivas con las inasistencias del año.
public static class PromediosBoletin
{
    internal static IReadOnlyList<PromedioBoletin> Calcular(IReadOnlyList<Lapso> lapsos, IReadOnlyList<ResultadoCerrado> resultados,
        IReadOnlyList<MateriaBoletin> materias, PoliticaAcademica? politica, bool cerrado)
    {
        var decimales = politica?.Reglas.DecimalesResultado;
        var promedios = lapsos.Select(lapso =>
        {
            var promedio = CalculoPromedio.DeMomento(politica, resultados.Select(r => r.Lapsos.FirstOrDefault(l => l.Orden == lapso.Orden) is { } nota
                ? (r.Modalidad, nota.Tipo, nota.Valor)
                : (r.Modalidad, TipoResultado.Pendiente, (decimal?)null)));
            var faltas = Sumar(materias.SelectMany(m => m.Notas).Where(n => n.LapsoId == lapso.Id).Select(n => n.Inasistencias));
            return Fila(lapso.Id, lapso.Orden, promedio, decimales, faltas);
        }).ToList();
        if (cerrado)
        {
            var promedio = CalculoPromedio.DeDefinitivas(politica, resultados.Select(r => (r.Modalidad, r.Definitiva)));
            promedios.Add(Fila(null, null, promedio, decimales, Sumar(promedios.Select(p => p.Inasistencias))));
        }
        return promedios;
    }

    private static PromedioBoletin Fila(Guid? lapsoId, int? orden, Definitiva promedio, int? decimales, int? faltas) =>
        new(lapsoId, orden, promedio.Estado, promedio.Valor, FormatoResultado.Definitiva(promedio.Estado, promedio.Valor, decimales,
            abreviado: true), faltas);

    // Nulo si nadie registró inasistencias; así un total vacío no se confunde con cero faltas.
    public static int? Sumar(IEnumerable<int?> valores)
    {
        var registrados = valores.Where(v => v is not null).Select(v => v!.Value).ToList();
        return registrados.Count == 0 ? null : registrados.Sum();
    }
}
