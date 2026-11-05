using System.Globalization;
using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Presentation.Models;
public enum DecisionNota
{
    ConservarLocal,
    UsarServidor,
    DescartarCopia
}

public sealed record OpcionDecisionNota(DecisionNota Valor, string Texto);
public sealed class FilaComparacionNota : ModeloObservable
{
    // Una coincidencia de valores no acredita que el intento anterior se guardó: la decisión siempre es explícita.
    private OpcionDecisionNota? decision;
    public FilaComparacionNota(FilaNota local, FilaPlanillaRemota? remota, PlanillaRemota? planilla = null)
    {
        Local = local;
        Remota = remota;
        var compatible = remota is not null && remota.Tipo != 4 && (planilla is null || (planilla.Modalidad ?? 2) == local.Modalidad
            && (local.Letra is null || planilla.Letras.Any(l => l.Letra == local.Letra)));
        Opciones = remota is null ? [new(DecisionNota.DescartarCopia, "Retirar de esta copia")]
            : compatible ? [new(DecisionNota.ConservarLocal, "Conservar mi edición"), new(DecisionNota.UsarServidor, "Usar resultado del servidor")]
            : [new(DecisionNota.UsarServidor, "Usar resultado del servidor · evaluación modificada")];
    }

    public FilaNota Local { get; }
    public FilaPlanillaRemota? Remota { get; }
    public string Estudiante => Local.Estudiante;
    public string MiNota => Resultado(Local.Valor, Local.Letra, Local.Exonerada ? 4 : 0);
    public string NotaServidor => Remota is null ? "Fuera de la nómina" : Resultado(Remota.Valor, Remota.Letra, Remota.Tipo ?? 0);
    private static string Resultado(decimal? numero, string? letra, int tipo) => tipo == 4 ? "EX · Exonerado"
        : numero is { } valor ? valor.ToString("0.##", CultureInfo.GetCultureInfo("es-VE")) + (letra is null ? "" : " (" + letra + ")") : letra ?? "Pendiente";
    public IReadOnlyList<OpcionDecisionNota> Opciones { get; }
    public OpcionDecisionNota? Decision { get => decision; set => Establecer(ref decision, Opciones.Contains(value!) ? value : null); }
}
