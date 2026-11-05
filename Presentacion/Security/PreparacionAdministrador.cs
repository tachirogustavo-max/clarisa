using Clarisa.Application.Common.Errores;
using Clarisa.Application.Features.Cuentas;

namespace Clarisa.Api.Security;

// Procedimiento local del servidor para crear la primera cuenta de administración. No existe ruta HTTP equivalente.
public static class PreparacionAdministrador
{
    public const string Argumento = "--crear-administrador";

    public static async Task<bool> EjecutarSiSeSolicitaAsync(WebApplication aplicacion, string[] argumentos)
    {
        ArgumentNullException.ThrowIfNull(aplicacion);
        if (!argumentos.Contains(Argumento, StringComparer.Ordinal)) return false;
        Environment.ExitCode = await EjecutarAsync(aplicacion.Services, new ConsolaSistema(), CancellationToken.None);
        return true;
    }

    public static async Task<int> EjecutarAsync(IServiceProvider servicios, IConsolaPreparacion consola, CancellationToken ct)
    {
        if (consola.EntradaRedirigida)
        {
            consola.Escribir("La preparación requiere una consola interactiva en el servidor.\n");
            return 2;
        }
        await using var alcance = servicios.CreateAsyncScope();
        var preparacion = alcance.ServiceProvider.GetRequiredService<PreparacionCuentas>();
        try
        {
            if (await preparacion.HayCuentasAsync(ct))
            {
                consola.Escribir("Ya existen cuentas. Usa la administración de usuarios desde una sesión de dirección.\n");
                return 3;
            }
            consola.Escribir("Usuario de la dirección: ");
            var usuario = consola.LeerLinea();
            consola.Escribir("Nombre visible: ");
            var nombre = consola.LeerLinea();
            consola.Escribir("Contraseña: ");
            var clave = consola.LeerSecreto();
            consola.Escribir("Repite la contraseña: ");
            if (clave is null || !string.Equals(clave, consola.LeerSecreto(), StringComparison.Ordinal))
            {
                consola.Escribir("Las contraseñas no coinciden. No se creó la cuenta.\n");
                return 4;
            }
            await preparacion.CrearPrimerAdministradorAsync(new PrimerAdministrador(Guid.CreateVersion7(), usuario, nombre, clave), ct);
            consola.Escribir("Cuenta de administración creada.\n");
            return 0;
        }
        catch (ErrorAplicacionException error)
        {
            consola.Escribir($"No se creó la cuenta: {error.Message}\n");
            return 5;
        }
    }
}
