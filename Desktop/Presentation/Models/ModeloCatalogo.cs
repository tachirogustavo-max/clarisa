using Clarisa.Desktop.Presentation.Commands;
using Clarisa.Desktop.Presentation.Forms;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Models;
public interface IEditorCatalogo : ISalidaProtegida
{
    event EventHandler? RegistroGuardado;
    string? CampoInvalido { get; }

    void NuevoRegistro();
    void EditarSeleccionado();
    void CerrarEditor();
    void SeguirEditando();
    void DescartarFormulario();
    Task GuardarFormularioAsync();
}

public abstract class ModeloCatalogo<T, TFormulario> : ModeloListado<T>, IEditorCatalogo where TFormulario : FormularioRegistro
{
    private readonly IClienteCatalogo<T>? cliente;
    private readonly CancellationTokenSource cierreEdicion = new();
    private Action? salidaSolicitada;
    public event EventHandler? RegistroGuardado;
    public bool GuardadoConfirmado { get; private set; }

    protected ModeloCatalogo(IClienteCatalogo<T>? cliente) : base(cliente) => this.cliente = cliente;
    public TFormulario? Formulario { get; private set; }
    public string? CampoInvalido => Formulario?.PrimerCampoInvalido;
    public bool EditorVisible { get; private set; }
    public override bool EditorAbierto => EditorVisible;
    public bool GuardandoFormulario { get; private set; }
    public bool DescartarVisible { get; private set; }
    public string ErrorFormulario { get; protected set; } = "";
    public virtual string TituloFormulario => Formulario?.Nuevo == true ? TextoNuevo : "Editar registro";
    public virtual string TextoGuardarFormulario => "Guardar";

    protected abstract TFormulario CrearFormulario(T? registro);
    public void NuevoRegistro()
    {
        if (!EditorVisible)
            Abrir(CrearFormulario(default));
    }

    public void EditarSeleccionado()
    {
        if (!EditorVisible && Seleccionado is not null)
            Abrir(CrearFormulario(Seleccionado));
    }

    private void Abrir(TFormulario formulario)
    {
        Formulario = formulario;
        GuardadoConfirmado = false;
        EditorVisible = true;
        ErrorFormulario = "";
        DescartarVisible = false;
        ActualizarEditor();
        FormularioAbierto(formulario);
    }

    protected virtual void FormularioAbierto(TFormulario formulario)
    {
    }

    public void CerrarEditor()
    {
        if (GuardandoFormulario)
            return;
        if (Formulario?.HayCambios == true)
        {
            DescartarVisible = true;
            ActualizarEditor();
            return;
        }

        EditorVisible = false;
        ActualizarEditor();
    }

    public void SeguirEditando()
    {
        salidaSolicitada = null;
        DescartarVisible = false;
        ActualizarEditor();
    }

    public void DescartarFormulario()
    {
        if (GuardandoFormulario)
            return;
        DescartarVisible = false;
        EditorVisible = false;
        Formulario = null;
        var salida = salidaSolicitada;
        salidaSolicitada = null;
        ActualizarEditor();
        salida?.Invoke();
    }

    public bool SolicitarSalida(Action continuar)
    {
        if (!EditorVisible)
            return true;
        if (GuardandoFormulario)
        {
            ErrorFormulario = "Espera la respuesta del servidor antes de salir.";
            ActualizarEditor();
            return false;
        }

        if (Formulario?.HayCambios != true)
            return true;
        salidaSolicitada = continuar;
        DescartarVisible = true;
        ActualizarEditor();
        return false;
    }

    public override void Cancelar()
    {
        cierreEdicion.Cancel();
        base.Cancelar();
    }

    public virtual Task GuardarFormularioAsync() => ProcesarGuardadoAsync((formulario, ct) => cliente is null ? Task.FromResult(new ResultadoRemoto<RegistroConfirmado>(null, "No hay una sesión de servidor conectada. La edición se conserva.")) : cliente.GuardarAsync(formulario.Id, formulario.Nuevo, formulario.CrearSolicitud(), ct));
    protected async Task ProcesarGuardadoAsync(Func<TFormulario, CancellationToken, Task<ResultadoRemoto<RegistroConfirmado>>> guardar)
    {
        if (GuardandoFormulario || Formulario is null || !Formulario.Validar())
            return;
        GuardandoFormulario = true;
        ErrorFormulario = "";
        ActualizarEditor();
        var formulario = Formulario;
        var revisionEnviada = formulario.RevisionLocal;
        var confirmar = formulario.PrepararConfirmacion();
        try
        {
            var respuesta = await guardar(formulario, cierreEdicion.Token);
            if (respuesta.Datos is not { } datos || datos.Id != formulario.Id || datos.Version <= 0 || datos.Version < formulario.Version)
            {
                ErrorFormulario = respuesta.Error ?? "El servidor no confirmó el registro. La edición se conserva.";
                if (respuesta.Error is not null && formulario.RevisionLocal == revisionEnviada)
                    formulario.AplicarErrorServidor(respuesta.Campo, respuesta.Error);
                return;
            }

            confirmar(datos.Version);
            GuardadoConfirmado = true;
            RegistroGuardado?.Invoke(this, EventArgs.Empty);
            if (formulario.RevisionLocal != revisionEnviada)
            {
                ErrorFormulario = "El envío fue confirmado; conservamos los cambios que hiciste después para que los revises.";
                return;
            }

            EditorVisible = false;
            Formulario = null;
            Mensaje = "Registro confirmado por el servidor.";
            await CargarAsync();
        }
        catch (OperationCanceledException)when (cierreEdicion.IsCancellationRequested)
        {
        }
        finally
        {
            GuardandoFormulario = false;
            ActualizarEditor();
        }
    }

    protected void ActualizarEditor()
    {
        Notificar(nameof(GuardadoConfirmado));
        foreach (var nombre in new[]
        {
            nameof(Formulario),
            nameof(EditorVisible),
            nameof(GuardandoFormulario),
            nameof(ErrorFormulario),
            nameof(TituloFormulario),
            nameof(TextoGuardarFormulario),
            nameof(DescartarVisible)
        }

        )
            Notificar(nombre);
        Actualizar();
    }
}
