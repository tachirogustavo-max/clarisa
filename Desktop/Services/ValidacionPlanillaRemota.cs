namespace Clarisa.Desktop.Services;
public static class ValidacionPlanillaRemota
{
    public static bool EsValida(PlanillaRemota? datos, ContextoPlanilla contexto)
    {
        if (datos is null || datos.SeccionId != contexto.SeccionId || datos.MateriaId != contexto.MateriaId || datos.LapsoId != contexto.LapsoId
            || datos.DecimalesAdmitidos is < 0 or > 2 || datos.Modalidad is < 1 or > 4 || datos.Filas is null || datos.Filas.Count > 2000
            || datos.Letras is null || datos.Letras.Count > 200 || datos.Modalidad == ModalidadRemota.NoCursada && datos.CargaPermitida
            || !EscalaRemota.Valida(datos.EscalaMinima, datos.EscalaMaxima) || datos.ResultadosCerrados && datos.CargaPermitida) return false;
        if (datos.Letras.Any(l => l is null || l.Id == Guid.Empty || l.AnioLectivoId == Guid.Empty || l.Version < 1 || !LetraValida(l.Letra))
            || datos.Letras.Select(l => l.Letra).Distinct().Count() != datos.Letras.Count
            || datos.Letras.Select(l => l.AnioLectivoId).Distinct().Count() > 1) return false;
        return datos.Filas.Select(f => f?.InscripcionId).Distinct().Count() == datos.Filas.Count && datos.Filas.All(f => FilaValida(f, datos));
    }
    public static bool LetraValida(string? letra) => letra is { Length: >= 1 and <= 4 } && letra.All(c => c is >= 'A' and <= 'Z');
    private static bool FilaValida(FilaPlanillaRemota? fila, PlanillaRemota datos)
    {
        if (fila is null || fila.InscripcionId == Guid.Empty || fila.EstudianteId == Guid.Empty || fila.Version < 0
            || string.IsNullOrWhiteSpace(fila.Nombres) || fila.Nombres.Length > 80 || string.IsNullOrWhiteSpace(fila.Apellidos) || fila.Apellidos.Length > 80
            || fila.Valor is { } numero && (numero < datos.EscalaMinima || numero > datos.EscalaMaxima
                || decimal.Round(numero, datos.DecimalesAdmitidos) != numero)) return false;
        var tipo = fila.Tipo ?? (fila.Valor is null ? 0 : 1);
        var coherente = tipo switch
        {
            0 => fila.Valor is null && fila.Letra is null && fila.MotivoExoneracion is null,
            1 => fila.Valor is not null && fila.Letra is null && fila.MotivoExoneracion is null,
            2 => fila.Valor is null && LetraValida(fila.Letra) && fila.MotivoExoneracion is null,
            3 => fila.Valor is not null && LetraValida(fila.Letra) && fila.MotivoExoneracion is null,
            4 => fila.Valor is null && fila.Letra is null && (fila.MotivoExoneracion is null
                || fila.MotivoExoneracion is { Length: <= 200 } motivo && !string.IsNullOrWhiteSpace(motivo)),
            _ => false
        };
        if (!coherente || tipo != 0 && fila.Version < 1 || fila.Letra is { } letra && !datos.Letras.Any(l => l.Letra == letra)) return false;
        return tipo == TipoResultadoRemoto.Pendiente || datos.Modalidad switch
        {
            ModalidadRemota.NoCursada => false,
            ModalidadRemota.Cualitativa => tipo is TipoResultadoRemoto.Cualitativo or TipoResultadoRemoto.Exonerado,
            ModalidadRemota.NumericaYCualitativa => tipo is TipoResultadoRemoto.NumericoYCualitativo or TipoResultadoRemoto.Exonerado,
            _ => tipo is TipoResultadoRemoto.Numerico or TipoResultadoRemoto.Exonerado
        };
    }
}
