using Clarisa.Application.Features.Respaldos;
using Clarisa.Infrastructure.Configuration;
using Clarisa.Infrastructure.Respaldos.Claves;
using Clarisa.Infrastructure.Respaldos.Mantenimiento;
using Clarisa.Infrastructure.Respaldos.MariaDb;
using Clarisa.Infrastructure.Respaldos.Operaciones;

namespace Clarisa.Api.Configuration;

// Requiere ConfiguracionMariaDb e IReloj ya registrados por AgregarPersistenciaClarisa. Todo es de vida única: el trabajador nunca
// depende de servicios de una solicitud HTTP.
public static class ComposicionRespaldos
{
    public const string Seccion = "Respaldos";

    public static IServiceCollection AgregarRespaldosClarisa(this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddSingleton(LeerConfiguracion(configuracion));
        servicios.AddSingleton<AlmacenClaveRespaldos>();
        servicios.AddSingleton<IClaveRespaldos>(p => p.GetRequiredService<AlmacenClaveRespaldos>());
        servicios.AddSingleton<AlmacenCopiasArchivo>();
        servicios.AddSingleton<IAlmacenCopias>(p => p.GetRequiredService<AlmacenCopiasArchivo>());
        servicios.AddSingleton<IRegistroOperacionesRespaldo, RegistroOperacionesArchivo>();
        servicios.AddSingleton<IGeneradorCopias, GeneradorCopiasMariaDb>();
        servicios.AddSingleton<IVerificadorPermisoRespaldos, VerificadorPermisoRespaldosMariaDb>();
        servicios.AddSingleton<CoordinacionRespaldos>();
        servicios.AddSingleton<GestionRespaldos>();
        servicios.AddSingleton<EjecucionRespaldos>();
        servicios.AddSingleton<CopiasAutomaticas>();
        servicios.AddHostedService<TrabajadorRespaldos>();
        return servicios;
    }

    public static ConfiguracionRespaldos LeerConfiguracion(IConfiguration configuracion) =>
        (configuracion.GetSection(Seccion).Get<ConfiguracionRespaldos>()
            ?? throw new InvalidOperationException("Falta la configuración de respaldos del servidor: " + Seccion + ".")).Validar();

    // Para InicioServidor: se toma antes de abrir el puerto y se conserva hasta terminar el proceso.
    public static IDisposable AdquirirExclusion(ConfiguracionRespaldos respaldos) =>
        new ExclusionInstanciaArchivo(respaldos.ArchivoExclusion).Adquirir();

    // Para InicioServidor y --migrar: base que debe usar este arranque según la última activación local.
    public static string BaseActiva(ConfiguracionRespaldos respaldos, string baseConfigurada) =>
        ReferenciaBaseActivaArchivo.BaseParaArranque(respaldos.ArchivoBaseActiva, baseConfigurada);
}
