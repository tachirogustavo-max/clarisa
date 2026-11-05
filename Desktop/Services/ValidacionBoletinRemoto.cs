namespace Clarisa.Desktop.Services;
public static class ValidacionBoletinRemoto
{
    public static bool EsValido(BoletinRemoto? datos, Guid anio, Guid seccion, Guid estudiante, Guid? lapso)
    {
        if (datos is null || datos.InscripcionId == Guid.Empty || datos.EstudianteId != estudiante || datos.SeccionId != seccion
            || datos.AnioLectivoId != anio || datos.Materias is null || datos.Materias.Count > 200 || datos.Politica?.Pendientes is null
            || string.IsNullOrWhiteSpace(datos.Nombres) || string.IsNullOrWhiteSpace(datos.Apellidos)
            || !EscalaRemota.Valida(datos.Politica.EscalaMinima, datos.Politica.EscalaMaxima)) return false;
        if (datos.Materias.Select(m => m?.MateriaId).Distinct().Count() != datos.Materias.Count) return false;
        return datos.Materias.All(m => m is not null && m.MateriaId != Guid.Empty && !string.IsNullOrWhiteSpace(m.Nombre)
            && m.Modalidad is not (< 1 or > 4) && m.Notas is not null && m.Notas.Count <= 3
            && m.Notas.Select(n => n?.Orden).Distinct().Count() == m.Notas.Count
            && m.Notas.All(n => n is not null && n.LapsoId != Guid.Empty && n.Orden is >= 1 and <= 3
                && (lapso is null || lapso == Guid.Empty || n.LapsoId == lapso)
                && !string.IsNullOrWhiteSpace(n.Texto) && n.Texto.Length <= 40
                && FormatoResultadoRemoto.Coherente(n.Estado ?? (n.Valor is null ? 0 : 1), n.Valor, n.Letra, datos.Politica.EscalaMinima,
                    datos.Politica.EscalaMaxima))
            && (m.Definitiva is null || m.Definitiva.Texto.Length <= 40 && m.Definitiva.Estado is >= 0 and <= 4));
    }
}
