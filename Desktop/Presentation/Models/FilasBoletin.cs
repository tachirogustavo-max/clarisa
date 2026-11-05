using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Presentation.Models;

// Filas de la vista previa del boletín: una por materia y, al final, la fila PROMEDIO con el total de inasistencias.
internal static class FilasBoletin
{
    public static IReadOnlyList<FilaBoletin> Crear(BoletinRemoto boletin)
    {
        var filas = boletin.Materias.Select(m => new FilaBoletin(m.Nombre, Nota(m, 1), Nota(m, 2), Nota(m, 3), Definitiva(boletin, m),
            Faltas(m.Notas.Select(n => n.Inasistencias)))).ToList();
        if (boletin.Promedios is { Count: > 0 } promedios)
            filas.Add(new FilaBoletin("Promedio", Promedio(promedios, 1), Promedio(promedios, 2), Promedio(promedios, 3),
                promedios.FirstOrDefault(p => p.LapsoId is null)?.Texto ?? "",
                Faltas(promedios.Where(p => p.LapsoId is not null).Select(p => p.Inasistencias))) { EsPromedio = true });
        return filas;
    }

    private static string Nota(MateriaBoletinRemota materia, int orden)
    {
        var nota = materia.Notas.FirstOrDefault(n => n.Orden == orden);
        return nota is null ? "Sin consultar" : nota.Texto ?? FormatoResultadoRemoto.SinVerificar;
    }

    // Sin cierre la definitiva es provisional: puede cambiar mientras se corrigen notas.
    private static string Definitiva(BoletinRemoto boletin, MateriaBoletinRemota materia) => materia.Definitiva is not { } definitiva ? "Pendiente"
        : boletin.Cierre is null && definitiva.Valor is not null ? definitiva.Texto + " · provisional"
        : definitiva.Texto.Length == 0 ? "Sin definitiva numérica" : definitiva.Texto;

    private static string Promedio(IReadOnlyList<PromedioRemoto> promedios, int orden) =>
        promedios.FirstOrDefault(p => p.Orden == orden)?.Texto ?? "";

    // Vacío si nadie registró inasistencias: no es lo mismo que cero faltas.
    private static string Faltas(IEnumerable<int?> valores)
    {
        var registradas = valores.Where(v => v is not null).Select(v => v!.Value).ToList();
        return registradas.Count == 0 ? "" : registradas.Sum().ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
