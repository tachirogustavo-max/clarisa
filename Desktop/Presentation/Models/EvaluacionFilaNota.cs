using Clarisa.Desktop.Services;
namespace Clarisa.Desktop.Presentation.Models;
public sealed record EvaluacionFilaNota(int Modalidad, int Tipo, string? Letra, IReadOnlyList<string> Letras, string? MotivoExoneracion = null)
{
    public decimal EscalaMinima { get; init; } = EscalaRemota.Minima;
    public decimal EscalaMaxima { get; init; } = EscalaRemota.Maxima;
    public int? Inasistencias { get; init; }
}
public static class FabricaFilasNota
{
    // Escala y decimales salen de la planilla del servidor: el escritorio valida con los mismos límites sin repetirlos.
    public static FilaNota Crear(PlanillaRemota planilla, FilaPlanillaRemota fila) =>
        new(fila.InscripcionId, fila.Apellidos + ", " + fila.Nombres, fila.Cedula, fila.Valor, fila.Version, planilla.DecimalesAdmitidos,
            new(planilla.Modalidad ?? ModalidadRemota.Numerica, fila.Tipo ?? (fila.Valor is null ? TipoResultadoRemoto.Pendiente : TipoResultadoRemoto.Numerico),
                fila.Letra, planilla.Letras.Select(l => l.Letra).ToArray(), fila.MotivoExoneracion)
            {
                EscalaMinima = planilla.EscalaMinima,
                EscalaMaxima = planilla.EscalaMaxima,
                Inasistencias = fila.Inasistencias
            });
}
