using Clarisa.Api.Endpoints;
using Clarisa.Api.Security;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Features.Cuentas;
using Clarisa.Infrastructure.Configuration;
using Clarisa.Infrastructure.Persistence.Esquema;

namespace Clarisa.Api.Configuration;

public static class InicioServidor
{
    public const int Correcto = 0;
    public const int Fallo = 1;
    public const int ArgumentosInvalidos = 2;

    public static Task<int> EjecutarAsync(string[] argumentos) => EjecutarAsync(argumentos, CancellationToken.None);

    public static async Task<int> EjecutarAsync(string[] argumentos, CancellationToken detener)
    {
        try
        {
            var orden = OrdenServidor.Interpretar(argumentos);
            var configuracion = orden.DesdeEntorno ? ConfiguracionEntornoServidor.Leer(Environment.GetEnvironmentVariable)
                : orden.DesdeServicio ? EntornoServicioRegistrado.Leer()
                : ArchivoConfiguracionServidor.Leer(orden.RutaConfiguracion, AppContext.BaseDirectory);
            var constructor = ArranqueClarisa.CrearConstructor(configuracion);
            var respaldos = ComposicionRespaldos.LeerConfiguracion(constructor.Configuration);
            // La exclusión se toma antes de leer la referencia activa o migrar y se conserva hasta que termina el proceso.
            using var exclusion = ComposicionRespaldos.AdquirirExclusion(respaldos);
            var configurada = PersistenciaClarisa.LeerConfiguracion(constructor.Configuration, "DatosAplicacion").Base;
            var migrar = orden.Modo == ModoServidor.Migrar;
            if (migrar && !string.Equals(constructor.Configuration["DatosMigracion:Base"], configurada, StringComparison.Ordinal))
                throw new ConfiguracionServidorException("La migración exige la misma base que DatosAplicacion y un usuario de migración separado.");
            var activa = ComposicionRespaldos.BaseActiva(respaldos, configurada);
            ArranqueClarisa.FijarBaseActiva(constructor.Configuration, activa, migrar);
            if (migrar)
            {
                await PersistenciaClarisa.MigrarDatosAsync(constructor.Configuration, detener);
                Console.WriteLine($"Esquema de la base {activa} actualizado y verificado.");
                Console.WriteLine("Esquema de datos actualizado y verificado.");
                return Correcto;
            }
            Componer(constructor);
            await using var aplicacion = constructor.Build();
            await aplicacion.Services.VerificarDatosAsync(detener);
            if (orden.Modo == ModoServidor.CrearAdministradorEntrada)
            {
                await using var entrada = Console.OpenStandardInput();
                return await PreparacionAdministradorEntrada.EjecutarAsync(aplicacion.Services, Console.IsInputRedirected, entrada, Console.Out, detener);
            }
            if (await PreparacionAdministrador.EjecutarSiSeSolicitaAsync(aplicacion, argumentos)) return Environment.ExitCode;
            aplicacion.Logger.LogInformation("Base activa: {Base} ({Origen}).", activa,
                activa == configurada ? "configurada" : "referencia de recuperación");
            Canalizar(aplicacion);
            await aplicacion.RunAsync(detener);
            return Correcto;
        }
        catch (ArgumentosServidorException error)
        {
            Console.Error.WriteLine(error.Message);
            Console.Error.WriteLine(OrdenServidor.Uso);
            return ArgumentosInvalidos;
        }
        catch (Exception error) when (error is ConfiguracionServidorException or ErrorAplicacionException or ArgumentException
                                          or EsquemaNoDisponibleException or MigracionRechazadaException)
        {
            // Mensajes propios de texto fijo: no contienen valores del archivo ni claves.
            Console.Error.WriteLine("El servidor no inició: " + error.Message);
            return Fallo;
        }
        catch (OperationCanceledException) when (detener.IsCancellationRequested)
        {
            Console.Error.WriteLine("El servidor se detuvo antes de terminar de iniciar.");
            return Fallo;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("El servidor no inició (" + error.GetType().Name + "). Revisa la configuración del servidor.");
            return Fallo;
        }
    }

    private static void Componer(WebApplicationBuilder constructor)
    {
        constructor.Services.AgregarPersistenciaClarisa(constructor.Configuration);
        constructor.Services.AgregarTransporteClarisa();
        constructor.Services.AgregarSeguridadClarisa(constructor.Configuration);
        constructor.Services.AgregarDocumentosClarisa(constructor.Configuration);
        constructor.Services.AgregarRespaldosClarisa(constructor.Configuration);
        if (string.IsNullOrWhiteSpace(constructor.Configuration[WebHostDefaults.ServerUrlsKey]))
            constructor.WebHost.UseUrls("https://localhost:5189");
    }

    private static void Canalizar(WebApplication aplicacion)
    {
        aplicacion.Services.GetRequiredService<HuellaVerificacionSenuelo>().Preparar();
        aplicacion.UseExceptionHandler();
        aplicacion.UsarProteccionClarisa();
        aplicacion.UseAuthentication();
        aplicacion.UseAuthorization();
        aplicacion.MapearRutasClarisa();
    }
}
