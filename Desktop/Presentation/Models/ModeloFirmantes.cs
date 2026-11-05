using System.Runtime.CompilerServices;
using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Presentation.Models;

public class ModeloFirmantes : ModeloObservable, ISalidaProtegida
{
    private const int MaximoNombre = 120;
    private readonly IClienteFirmantes? cliente;
    private readonly CancellationTokenSource cierre = new();
    private FirmantesRemotos? actuales;
    private string director = "";
    private string coordinadorEvaluacion = "";
    private string coordinadorEstudios = "";
    private string motivo = "";

    public ModeloFirmantes(IClienteFirmantes? cliente = null) => this.cliente = cliente;

    public string Director { get => director; set => Cambiar(ref director, value); }
    public string CoordinadorEvaluacion { get => coordinadorEvaluacion; set => Cambiar(ref coordinadorEvaluacion, value); }
    public string CoordinadorEstudios { get => coordinadorEstudios; set => Cambiar(ref coordinadorEstudios, value); }
    public string Motivo { get => motivo; set => Cambiar(ref motivo, value); }
    public bool Consultado => actuales is not null;
    public bool Ocupado { get; private set; }
    public string Origen => actuales switch
    {
        null => "",
        { Numero: 0 } => "Son los nombres de la configuración del servidor. Al guardar, quedan registrados aquí.",
        { } f => $"Cambio n.º {f.Numero}" + (f.RegistradoEn is { } fecha ? $" del {fecha.ToLocalTime():dd/MM/yyyy HH:mm}" : "")
                 + (f.Motivo is { } m ? $" · {m}" : "")
    };
    public bool HayCambios => actuales is { } f && (Limpio(director) != Limpio(f.Nombres.Director)
        || Limpio(coordinadorEvaluacion) != Limpio(f.Nombres.CoordinadorEvaluacion) || Limpio(coordinadorEstudios) != Limpio(f.Nombres.CoordinadorEstudios));
    public string Mensaje { get; private set; } = "Consultando los firmantes…";
    public bool MensajeEsError { get; private set; }

    public Task ConsultarAsync() => EjecutarAsync(async ct =>
    {
        if (cliente is null) return false;
        var respuesta = await cliente.ConsultarAsync(ct);
        if (respuesta.Datos is not { } datos) return Informar(respuesta.Error ?? "No se pudieron consultar los firmantes.", true);
        Mostrar(datos);
        return Informar("Revisa los nombres antes de imprimir boletines.");
    });

    public Task GuardarAsync() => EjecutarAsync(async ct =>
    {
        if (cliente is null || actuales is not { } previos) return Informar("Consulta los firmantes antes de cambiarlos.", true);
        if (!HayCambios) return Informar("No hay cambios que guardar.");
        if (Error() is { } error) return Informar(error, true);
        var nombres = new NombresFirmantesRemotos(Limpio(director), Limpio(coordinadorEvaluacion), Limpio(coordinadorEstudios));
        var respuesta = await cliente.CambiarAsync(Guid.CreateVersion7(), previos.Numero, nombres, motivo.Trim(), ct);
        if (respuesta.Datos is not { } guardados) return Informar(respuesta.Error ?? "No se guardaron los firmantes.", true);
        Mostrar(guardados);
        return Informar("Firmantes guardados. Los boletines que se generen desde ahora llevan estos nombres.");
    });

    // Vuelve a leer del servidor: tras un rechazo por versión, muestra lo que guardó la otra persona.
    public Task DescartarAsync() => EjecutarAsync(async ct =>
    {
        if (cliente is null) return false;
        var respuesta = await cliente.ConsultarAsync(ct);
        if (respuesta.Datos is not { } datos) return Informar(respuesta.Error ?? "No se pudieron consultar los firmantes.", true);
        Mostrar(datos);
        return Informar("Se cargaron los nombres guardados en el servidor.");
    });

    public bool SolicitarSalida(Action continuar)
    {
        if (!HayCambios) return !Ocupado;
        Informar("Hay nombres sin guardar. Guárdalos o descarta los cambios antes de salir.", true);
        return false;
    }

    public void Cancelar() => cierre.Cancel();

    protected void Mostrar(FirmantesRemotos datos)
    {
        actuales = datos;
        director = datos.Nombres.Director ?? "";
        coordinadorEvaluacion = datos.Nombres.CoordinadorEvaluacion ?? "";
        coordinadorEstudios = datos.Nombres.CoordinadorEstudios ?? "";
        motivo = "";
        foreach (var propiedad in new[] { nameof(Director), nameof(CoordinadorEvaluacion), nameof(CoordinadorEstudios), nameof(Motivo),
                     nameof(Consultado), nameof(Origen), nameof(HayCambios) })
            Notificar(propiedad);
    }

    protected bool Informar(string mensaje, bool error = false)
    {
        Mensaje = mensaje;
        MensajeEsError = error;
        Notificar(nameof(Mensaje));
        Notificar(nameof(MensajeEsError));
        return !error;
    }

    private string? Error()
    {
        if (new[] { director, coordinadorEvaluacion, coordinadorEstudios }.All(n => Limpio(n) is null))
            return "Escribe al menos un nombre para las firmas.";
        if (new[] { director, coordinadorEvaluacion, coordinadorEstudios }.Any(n => (Limpio(n)?.Length ?? 0) > MaximoNombre))
            return $"Cada nombre admite hasta {MaximoNombre} caracteres.";
        return string.IsNullOrWhiteSpace(motivo) ? "Escribe el motivo del cambio (por ejemplo, el nombramiento)." : null;
    }

    private void Cambiar(ref string campo, string? valor, [CallerMemberName] string? propiedad = null)
    {
        if (!Establecer(ref campo, valor ?? "", propiedad)) return;
        Notificar(nameof(HayCambios));
    }

    private static string? Limpio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : string.Join(' ',
        valor.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private async Task EjecutarAsync(Func<CancellationToken, Task<bool>> accion)
    {
        if (Ocupado) return;
        Ocupado = true;
        Notificar(nameof(Ocupado));
        try
        {
            await accion(cierre.Token);
        }
        catch (OperationCanceledException) when (cierre.IsCancellationRequested)
        {
        }
        finally
        {
            Ocupado = false;
            Notificar(nameof(Ocupado));
        }
    }
}
