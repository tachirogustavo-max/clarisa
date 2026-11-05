using Clarisa.Application.Common.Errores;
using Clarisa.Infrastructure.Configuration;
using Clarisa.Infrastructure.Persistence.Esquema;
using Clarisa.Mantenimiento;

Console.OutputEncoding = new System.Text.UTF8Encoding(false);
if (args.Length == 0 || args[0] is "-h" or "--ayuda" or "ayuda")
{
    Console.WriteLine(Comandos.Uso);
    return args.Length == 0 ? 2 : 0;
}

using var cancelacion = new CancellationTokenSource();
Console.CancelKeyPress += (_, evento) =>
{
    evento.Cancel = true;
    cancelacion.Cancel();
};

try
{
    var opciones = Opciones.Interpretar(args.Skip(1).ToList());
    await new Comandos(ConfiguracionMantenimiento.Cargar(opciones.Valor("configuracion"), opciones.Valor("entorno") is not null,
        opciones.Valor("entorno-servicio") is not null)).EjecutarAsync(args[0], opciones, cancelacion.Token);
    return 0;
}
catch (ConfiguracionServidorException error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
catch (Exception error) when (error is MigracionRechazadaException or EsquemaNoDisponibleException)
{
    Console.Error.WriteLine("No se pudo verificar el esquema de la base activa. No se modificó la cuenta; revisa el estado y la versión del servidor.");
    return 1;
}
catch (ErrorAplicacionException error)
{
    Console.Error.WriteLine(error.Tipo == TipoError.ResultadoDesconocido
        ? "No se pudo confirmar el resultado. Pudo haberse aplicado: no repitas automáticamente; comprueba la operación antes de continuar."
        : $"No se completó ({error.Tipo}): {error.Message}");
    return 1;
}
catch (OperationCanceledException error)
{
    Console.Error.WriteLine(cancelacion.IsCancellationRequested ? "Operación cancelada." : error.Message);
    return 1;
}
catch (ArgumentException error)
{
    Console.Error.WriteLine(error.Message);
    Console.Error.WriteLine(Comandos.Uso);
    return 2;
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
{
    Console.Error.WriteLine($"No se completó por un fallo del sistema ({error.GetType().Name}). La base activa no se modificó salvo que se indicara antes.");
    return 1;
}
