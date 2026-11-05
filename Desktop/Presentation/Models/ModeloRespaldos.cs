using Clarisa.Desktop.Services;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Models;
public class ModeloRespaldos : ModeloListado<RespaldoRemoto>
{
    private readonly IClienteRespaldos? operaciones;
    private readonly IClienteDescargaRespaldos? descargas;
    private readonly IClienteGuiaRestauracion? guia;
    private readonly CancellationTokenSource cierreOperacion = new();
    private Guid? operacionId;
    private string tipoOperacion = "";
    public ModeloRespaldos(IClienteCatalogo<RespaldoRemoto>? lista = null, IClienteRespaldos? operaciones = null,
        IClienteDescargaRespaldos? descargas = null, IClienteGuiaRestauracion? guia = null) : base(lista)
    {
        this.operaciones = operaciones;
        this.descargas = descargas;
        this.guia = guia;
        Titulo = "Respaldos";
        Descripcion = "Copias del servidor y recuperación de la información académica.";
    }

    public bool GuiaVisible { get; private set; }
    public bool Descargando { get; private set; }
    public string MensajeGuia { get; private set; } = "La recuperación se prepara en el servidor con la herramienta de mantenimiento. Esta estación no sustituye datos.";
    public IReadOnlyList<string> PasosGuia { get; private set; } =
    ["Conserva una copia cifrada y su clave de recuperación fuera del servidor.",
     "El operador debe preparar y comprobar la copia en una base nueva.",
     "La base actual se conserva. La activación requiere mantenimiento local y reinicio del servidor."];
    public bool Ocupado { get; private set; }
    public bool Pendiente { get; private set; }
    public string EstadoOperacion { get; private set; } = "";
    public string DetalleOperacion { get; private set; } = "";
    public bool TieneOperacion => operacionId is not null;

    public async Task PedirRestauracionAsync()
    {
        GuiaVisible = true;
        ActualizarOperacion();
        if (guia is null || cierreOperacion.IsCancellationRequested) return;
        try
        {
            var respuesta = await guia.ConsultarGuiaAsync(cierreOperacion.Token);
            cierreOperacion.Token.ThrowIfCancellationRequested();
            if (respuesta.Datos is { Disponible: false } datos && datos.Pasos is { Length: > 0 and <= 20 })
            {
                MensajeGuia = datos.Mensaje;
                PasosGuia = datos.Pasos;
                ActualizarOperacion();
            }
        }
        catch (OperationCanceledException) when (cierreOperacion.IsCancellationRequested) { }
    }

    public void CerrarGuia()
    {
        GuiaVisible = false;
        ActualizarOperacion();
    }

    public async Task DescargarAsync(string destino)
    {
        if (Descargando || cierreOperacion.IsCancellationRequested) return;
        if (Seleccionado is not { Verificado: true } copia || descargas is null)
        {
            Mensaje = "Selecciona una copia verificada y conecta con el servidor.";
            Actualizar();
            return;
        }
        Descargando = true;
        Mensaje = "Guardando la copia…";
        ActualizarOperacion();
        try
        {
            var respuesta = await descargas.GuardarAsync(copia, destino, cierreOperacion.Token);
            cierreOperacion.Token.ThrowIfCancellationRequested();
            Mensaje = respuesta.Guardado ? "Copia guardada. Conserva también la clave de recuperación en un lugar seguro." : respuesta.Error ?? "No se guardó la copia.";
        }
        catch (OperationCanceledException) when (cierreOperacion.IsCancellationRequested) { }
        finally { Descargando = false; ActualizarOperacion(); }
    }

    public async Task CrearAsync()
    {
        if (Ocupado || Pendiente || cierreOperacion.IsCancellationRequested)
            return;
        if (operaciones is null)
        {
            Mensaje = "Conecta con el servidor para gestionar los respaldos.";
            Actualizar();
            return;
        }

        operacionId = Guid.CreateVersion7();
        tipoOperacion = "Respaldo";
        Ocupado = true;
        Pendiente = true;
        EstadoOperacion = "Esperando confirmación";
        ActualizarOperacion();
        try
        {
            var respuesta = await operaciones.IniciarAsync(operacionId.Value, cierreOperacion.Token);
            cierreOperacion.Token.ThrowIfCancellationRequested();
            if (respuesta.Rechazada)
            {
                Pendiente = false;
                EstadoOperacion = "Solicitud rechazada";
                DetalleOperacion = respuesta.Error ?? "El servidor no aceptó la solicitud.";
            }
            else Recibir(new(respuesta.Datos, respuesta.Error));
        }
        catch (OperationCanceledException)when (cierreOperacion.IsCancellationRequested)
        {
        }
        finally
        {
            Ocupado = false;
            ActualizarOperacion();
        }
    }

    public async Task ComprobarAsync()
    {
        if (Ocupado || operaciones is null || operacionId is null || cierreOperacion.IsCancellationRequested)
            return;
        Ocupado = true;
        ActualizarOperacion();
        try
        {
            var respuesta = await operaciones.ConsultarAsync(operacionId.Value, cierreOperacion.Token);
            cierreOperacion.Token.ThrowIfCancellationRequested();
            Recibir(respuesta);
        }
        catch (OperationCanceledException)when (cierreOperacion.IsCancellationRequested)
        {
        }
        finally
        {
            Ocupado = false;
            ActualizarOperacion();
        }

        if (!Pendiente)
            await CargarAsync();
    }

    private void Recibir(ResultadoRemoto<OperacionRespaldo> respuesta)
    {
        if (respuesta.Datos is not { } datos || datos.Id != operacionId || datos.Tipo != tipoOperacion || datos.Estado is not ("Pendiente" or "EnCurso" or "Completa" or "Fallida"))
        {
            EstadoOperacion = "Estado por comprobar";
            DetalleOperacion = respuesta.Error ?? "No llegó una confirmación válida. Consulta el estado; no se repetirá la orden.";
            return;
        }

        Pendiente = datos.Estado is "Pendiente" or "EnCurso";
        EstadoOperacion = datos.Estado switch
        {
            "EnCurso" => "Operación en curso",
            "Completa" => "Operación completada",
            "Fallida" => "La operación no se completó",
            _ => "Operación recibida"
        };
        DetalleOperacion = datos.Mensaje;
    }

    private void ActualizarOperacion()
    {
        foreach (var campo in new[]
        {
            nameof(GuiaVisible),
            nameof(MensajeGuia),
            nameof(PasosGuia),
            nameof(Descargando),
            nameof(Ocupado),
            nameof(Pendiente),
            nameof(EstadoOperacion),
            nameof(DetalleOperacion),
            nameof(TieneOperacion)
        }

        )
            Notificar(campo);
        Actualizar();
    }

    public override void Cancelar()
    {
        cierreOperacion.Cancel();
        base.Cancelar();
    }
}
