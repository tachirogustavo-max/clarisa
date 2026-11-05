namespace Clarisa.Desktop.Services;
public static class ValidacionConfirmacionNotas
{
    public static bool EsValida(IReadOnlyList<NotaParaGuardar> enviadas, IReadOnlyList<NotaConfirmada>? recibidas)
    {
        if (recibidas is null || recibidas.Count != enviadas.Count || recibidas.Any(n => n is null)
            || recibidas.Select(n => n.InscripcionId).Distinct().Count() != enviadas.Count) return false;
        var porId = recibidas.ToDictionary(n => n.InscripcionId);
        return enviadas.All(n => porId.TryGetValue(n.InscripcionId, out var r) && r.Valor == n.Valor && r.Letra == n.Letra
            && (!n.IncluyeInasistencias || r.Inasistencias == n.Inasistencias)
            && r.Version > 0 && r.Version >= n.VersionEsperada
            && (r.Tipo ?? (r.Valor is null ? r.Letra is null ? 0 : 2 : r.Letra is null ? 1 : 3))
                == (n.Valor is null ? n.Letra is null ? 0 : 2 : n.Letra is null ? 1 : 3));
    }
}
