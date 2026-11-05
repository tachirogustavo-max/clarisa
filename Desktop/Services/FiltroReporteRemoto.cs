namespace Clarisa.Desktop.Services;

public sealed record FiltroReporteRemoto(Guid? AnioLectivoId, Guid? SeccionId, Guid? EstudianteId, Guid? LapsoId,
    Guid? MateriaId, Guid? DocenteId, string Texto);

internal static class RutasReportes
{
    internal static string Crear(FiltroReporteRemoto filtro, bool pdf, int pagina = 1, int tamanio = 25)
    {
        var ruta = "api/reportes/academicos" + (pdf ? "/pdf" : "") + "?texto=" + Uri.EscapeDataString(filtro.Texto);
        if (!pdf) ruta += "&pagina=" + pagina + "&tamanio=" + tamanio;
        foreach (var (nombre, valor) in new[] {
            ("anioLectivoId", filtro.AnioLectivoId), ("seccionId", filtro.SeccionId), ("estudianteId", filtro.EstudianteId),
            ("lapsoId", filtro.LapsoId), ("materiaId", filtro.MateriaId), ("docenteId", filtro.DocenteId) })
            if (valor is { } id) ruta += "&" + nombre + "=" + id;
        return ruta;
    }
}
