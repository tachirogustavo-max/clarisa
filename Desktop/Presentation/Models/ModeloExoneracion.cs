using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Presentation.Models;
public sealed class ModeloExoneracion(IClienteExoneraciones cliente, ContextoPlanilla contexto,
    Func<bool> puedeActuar, Action<NotaConfirmada, string> confirmar, Action revisar) : ModeloObservable
{
    private readonly CancellationTokenSource cierre = new();
    private FilaNota? fila;
    private string motivo = "";
    public bool Visible { get; private set; }
    public bool Guardando { get; private set; }
    public bool Disponible => !Guardando;
    public bool DebeConsultar { get; private set; }
    public string Mensaje { get; private set; } = "";
    public string ErrorMotivo { get; private set; } = "";
    public string AvisoSalida { get; private set; } = "";
    public string Estudiante => fila?.Estudiante ?? "";
    public string MotivoAnterior => fila?.MotivoExoneracion is { } anterior ? "Motivo registrado: " + anterior : "";
    public string Accion => fila?.Exonerada == true ? "Revocar exoneración" : "Registrar exoneración";
    public string Explicacion => fila?.Exonerada == true ? "El resultado quedará pendiente. No se restaurarán notas anteriores."
        : "Se registrará EX sin valor numérico. El resultado anterior permanecerá en el historial.";
    public string Motivo { get => motivo; set => Establecer(ref motivo, value ?? ""); }
    public void Abrir(FilaNota seleccionada)
    {
        if (Visible || Guardando) return;
        fila = seleccionada;
        Visible = true;
        Motivo = "";
        ErrorMotivo = "";
        AvisoSalida = "";
        DebeConsultar = false;
        Mensaje = puedeActuar() && fila.Modalidad != 1 ? "" : "Primero guarda o descarta las ediciones y comprueba que la carga esté abierta.";
        Actualizar();
    }
    public async Task GuardarAsync()
    {
        if (Guardando || DebeConsultar || !Visible || fila is null || cierre.IsCancellationRequested) return;
        ErrorMotivo = string.IsNullOrWhiteSpace(Motivo) || Motivo.Trim().Length > 200 || Motivo.Any(char.IsControl)
            ? "Escribe un motivo de hasta 200 caracteres." : "";
        if (!puedeActuar() || fila.Modalidad == 1 || ErrorMotivo.Length > 0)
        {
            Mensaje = "Revisa el motivo, las ediciones pendientes y la ventana de carga.";
            Actualizar();
            return;
        }
        Guardando = true;
        Mensaje = "Confirmando con el servidor…";
        Actualizar();
        try
        {
            var revocar = fila.Exonerada;
            var respuesta = await cliente.CambiarAsync(contexto, fila.InscripcionId, fila.Version, Motivo.Trim(), revocar, cierre.Token);
            cierre.Token.ThrowIfCancellationRequested();
            if (respuesta.Datos is not { } nota || nota.InscripcionId != fila.InscripcionId || nota.Tipo != (revocar ? 0 : 4)
                || nota.Valor is not null || nota.Letra is not null || nota.Version < 1 || nota.Version < fila.Version)
            {
                Mensaje = respuesta.Error ?? "La confirmación llegó incompleta. Cierra y consulta la planilla antes de repetir.";
                DebeConsultar = respuesta.Campo != "Motivo";
                if (DebeConsultar) revisar();
                return;
            }
            confirmar(nota, Motivo.Trim());
            Visible = false;
            Mensaje = "Acto administrativo confirmado.";
        }
        catch (OperationCanceledException) when (cierre.IsCancellationRequested) { }
        finally { Guardando = false; Actualizar(); }
    }
    public void AvisarSalidaBloqueada()
    {
        if (!Visible) return;
        AvisoSalida = Guardando ? "Espera la respuesta del servidor antes de salir de la planilla."
            : "Confirma o cierra la exoneración antes de salir de la planilla.";
        Notificar(nameof(AvisoSalida));
    }
    public void Cerrar()
    {
        if (Guardando) return;
        Visible = false;
        Actualizar();
    }
    public void Cancelar() => cierre.Cancel();
    private void Actualizar()
    {
        foreach (var propiedad in new[] { nameof(Visible), nameof(Guardando), nameof(Disponible), nameof(DebeConsultar), nameof(Mensaje),
            nameof(ErrorMotivo), nameof(AvisoSalida), nameof(Estudiante), nameof(MotivoAnterior), nameof(Accion), nameof(Explicacion) }) Notificar(propiedad);
    }
}
