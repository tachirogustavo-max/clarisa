using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Application.Configuration;
using Clarisa.Infrastructure.Configuration;
using Clarisa.Infrastructure.Persistence;
using Clarisa.Infrastructure.Persistence.Esquema;

namespace Clarisa.Api.Configuration;

public static class PersistenciaClarisa
{
    public static IServiceCollection AgregarPersistenciaClarisa(this IServiceCollection servicios, IConfiguration configuracion)
    {
        var datos = LeerConfiguracion(configuracion, "DatosAplicacion");
        servicios.AddSingleton(datos);
        servicios.AddScoped<IFabricaUnidadTrabajo, FabricaUnidadTrabajoAutenticadaMariaDb>();
        servicios.AddScoped<IFabricaLecturasAcademicas>(p => new FabricaLecturasAcademicasMariaDb(
            p.GetRequiredService<ConfiguracionMariaDb>(), p.GetRequiredService<IReferenciaSesionActual>(), p.GetRequiredService<IReloj>()));
        servicios.AddSingleton<IReloj, RelojServidor>();
        foreach (var casoDeUso in CasosDeUso.Academicos) servicios.AddTransient(casoDeUso);
        return servicios;
    }

    public static ConfiguracionMariaDb LeerConfiguracion(IConfiguration configuracion, string seccion)
    {
        var datos = configuracion.GetSection(seccion).Get<ConfiguracionMariaDb>()
            ?? throw new InvalidOperationException("Falta la configuración de datos del servidor: " + seccion + ".");
        datos.Validar();
        return datos;
    }

    public static Task VerificarDatosAsync(this IServiceProvider servicios, CancellationToken ct) =>
        new MigradorEsquemaMariaDb(servicios.GetRequiredService<ConfiguracionMariaDb>()).VerificarAsync(ct);

    public static async Task MigrarDatosAsync(IConfiguration configuracion, CancellationToken ct)
    {
        var datos = LeerConfiguracion(configuracion, "DatosAplicacion");
        var migracion = LeerConfiguracion(configuracion, "DatosMigracion");
        if (datos.Servidor != migracion.Servidor || datos.Puerto != migracion.Puerto || datos.Base != migracion.Base
            || string.Equals(datos.Usuario, migracion.Usuario, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La migración exige la misma base y un usuario de migración separado.");
        await new MigradorEsquemaMariaDb(migracion).AplicarAsync(ct);
    }
}

internal sealed class RelojServidor : IReloj
{
    public DateTimeOffset Ahora => DateTimeOffset.UtcNow;
}
