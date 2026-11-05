using System.Security;
using Clarisa.Desktop.Presentation.Forms;
using Clarisa.Desktop.Services;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Models;
public interface IEditorConClave
{
    Task GuardarConClaveAsync(SecureString clave);
}

public class ModeloUsuarios : ModeloCatalogo<CuentaRemota, FormularioCuenta>, IEditorConClave
{
    private readonly IClienteCuentas? cuentas;
    public ModeloUsuarios(IClienteCatalogo<CuentaRemota>? lista = null, IClienteCuentas? cuentas = null) : base(lista)
    {
        this.cuentas = cuentas;
        Titulo = "Usuarios";
        Descripcion = "Cuentas de Dirección y de los docentes del liceo.";
        TextoNuevo = "Crear usuario";
    }

    public IReadOnlyList<OpcionRemota> Docentes { get; private set; } = [];

    public void EstablecerDocentes(IEnumerable<OpcionRemota> docentes) => Docentes = docentes.ToArray();
    protected override FormularioCuenta CrearFormulario(CuentaRemota? registro) => new(Docentes, registro);
    public override Task GuardarFormularioAsync()
    {
        ErrorFormulario = "Revisa el campo de contraseña antes de guardar la cuenta.";
        ActualizarEditor();
        return Task.CompletedTask;
    }

    public Task GuardarConClaveAsync(SecureString clave)
    {
        if (Formulario?.Nuevo == true && clave.Length == 0)
        {
            ErrorFormulario = "Escribe una contraseña para la nueva cuenta.";
            ActualizarEditor();
            return Task.CompletedTask;
        }

        return ProcesarGuardadoAsync((formulario, ct) => cuentas is null ? Task.FromResult(new ResultadoRemoto<RegistroConfirmado>(null, "No hay una sesión de servidor conectada. La edición se conserva.")) : cuentas.GuardarAsync((CambioCuenta)formulario.CrearSolicitud(), formulario.Nuevo, clave, ct));
    }
}
