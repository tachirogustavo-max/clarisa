using System.Security.Principal;
using System.Runtime.Versioning;
using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Application.Features.Cuentas;
using Clarisa.Infrastructure.Persistence;
using Clarisa.Infrastructure.Persistence.Esquema;
using Clarisa.Infrastructure.Respaldos.Mantenimiento;
using Clarisa.Infrastructure.Seguridad;
using Clarisa.Infrastructure.Configuration;
using Clarisa.Application.Common.Errores;

namespace Clarisa.Mantenimiento;

internal static class RecuperarCuenta
{
    public static async Task EjecutarAsync(ConfiguracionMantenimiento configuracion, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows()) throw new ArgumentException("La recuperación se ejecuta en el servidor Windows.");
        var identidad = ExigirAdministradorLocal();
        using var exclusion = new ExclusionInstanciaArchivo(configuracion.Respaldos.ArchivoExclusion).Adquirir();
        var activa = ReferenciaBaseActivaArchivo.BaseParaArranque(configuracion.Respaldos.ArchivoBaseActiva, configuracion.Datos.Base);
        var aplicacion = configuracion.Datos with { Base = activa };
        Console.WriteLine("Recuperar contraseña de administración. No crea cuentas ni cambia permisos.");
        Console.Write("Usuario de la aplicación: ");
        var usuario = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Motivo de la recuperación (sin contraseñas ni datos sensibles): ");
        var motivo = Console.ReadLine() ?? "";
        if (!EntradaOperador.Confirmar($"Se cerrarán todas las sesiones de {usuario}.", "RECUPERAR " + usuario))
            throw new OperationCanceledException("Recuperación cancelada por el operador.");
        var credencial = EntradaOperador.PedirCredencial();
        var operador = aplicacion with { Usuario = credencial.Usuario, Clave = credencial.Clave, MaximoConexiones = 1 };
        var fabrica = new FabricaRecuperacionCuentaMariaDb(aplicacion, operador);
        await new MigradorEsquemaMariaDb(operador).VerificarAsync(ct);
        var rutaLista = string.IsNullOrWhiteSpace(configuracion.Seguridad.ArchivoClavesProhibidas)
            ? "Resources/claves-prohibidas.txt" : configuracion.Seguridad.ArchivoClavesProhibidas;
        var prohibidas = CargarLista(rutaLista);
        var clave = EntradaOperador.PedirClaveNueva();
        var id = Guid.CreateVersion7();
        Console.WriteLine($"Operación {id}. Conserva este identificador si se interrumpe la confirmación.");
        var caso = new RecuperacionCuentaLocal(fabrica, new ProtectorClavesPbkdf2(configuracion.Seguridad.IteracionesClave), prohibidas, new RelojRecuperacion());
        await caso.RecuperarAsync(new(identidad, credencial.Usuario), new(id, usuario, clave, motivo), ct);
        Console.WriteLine("Contraseña restablecida y sesiones revocadas. Inicia el servidor y comprueba el acceso con la nueva contraseña.");
    }

    [SupportedOSPlatform("windows")]
    private static string ExigirAdministradorLocal()
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
            throw new ArgumentException("Abre una consola interactiva elevada en el servidor; no se admiten entradas ni salidas redirigidas.");
        using var identidad = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identidad).IsInRole(WindowsBuiltInRole.Administrator) || identidad.User is null)
            throw Error.AccesoDenegado("La recuperación requiere una consola ejecutada como administrador de Windows.");
        return identidad.User.Value;
    }

    private static ListaClavesProhibidasArchivo CargarLista(string ruta)
    {
        try { return ListaClavesProhibidasArchivo.Cargar(Path.GetFullPath(ruta, AppContext.BaseDirectory)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        { throw new ConfiguracionServidorException("No se pudo cargar la lista de contraseñas prohibidas. No se modificó la cuenta."); }
    }

    private sealed class RelojRecuperacion : IReloj
    {
        public DateTimeOffset Ahora => DateTimeOffset.UtcNow;
    }
}
