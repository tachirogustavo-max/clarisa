using Clarisa.Api.Security;
using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Application.Abstractions;
using Clarisa.Application.Configuration;
using Clarisa.Application.Features.Cuentas;
using Clarisa.Infrastructure.Configuration;
using Clarisa.Infrastructure.Persistence;
using Clarisa.Infrastructure.Seguridad;
using Microsoft.AspNetCore.Authorization;

namespace Clarisa.Api.Configuration;

// Requiere ConfiguracionMariaDb (usuario de la aplicación) e IReloj ya registrados por la composición de persistencia.
public static class SeguridadClarisa
{
    public const string Esquema = "SesionClarisa";

    public static IServiceCollection AgregarSeguridadClarisa(this IServiceCollection servicios, IConfiguration configuracion)
    {
        var opciones = OpcionesSeguridad.Leer(configuracion);
        servicios.AddSingleton(opciones);
        servicios.AddSingleton(new OpcionesSesiones { HorasSesion = opciones.HorasSesion }.Validar());
        var limites = new OpcionesLimiteIntentos
        {
            VerificacionesSimultaneas = opciones.VerificacionesSimultaneas ?? Math.Clamp(Environment.ProcessorCount / 2, 1, 8)
        };
        servicios.AddSingleton(limites.Validar());
        servicios.AddSingleton<TurnoVerificacionClaves>();
        servicios.AddScoped<IFabricaUnidadSeguridad>(proveedor =>
            new FabricaUnidadSeguridadMariaDb(proveedor.GetRequiredService<ConfiguracionMariaDb>(),
                proveedor.GetRequiredService<IReferenciaSesionActual>(), proveedor.GetRequiredService<IReloj>()));
        servicios.AddSingleton<IProtectorClaves>(new ProtectorClavesPbkdf2(opciones.IteracionesClave));
        var archivoProhibidas = string.IsNullOrWhiteSpace(opciones.ArchivoClavesProhibidas)
            ? "Resources/claves-prohibidas.txt" : opciones.ArchivoClavesProhibidas;
        servicios.AddSingleton<IListaClavesProhibidas>(ListaClavesProhibidasArchivo.Cargar(
            Path.GetFullPath(archivoProhibidas, AppContext.BaseDirectory)));
        servicios.AddSingleton<IGeneradorTokensSesion, GeneradorTokensSesion>();
        servicios.AddSingleton<HuellaVerificacionSenuelo>();
        servicios.AddSingleton<LimitadorIntentos>();
        servicios.AddScoped<AccesoCuentas>();
        foreach (var casoDeUso in CasosDeUso.Cuentas) servicios.AddTransient(casoDeUso);

        servicios.AddAuthentication(autenticacion =>
            {
                autenticacion.DefaultScheme = Esquema;
                autenticacion.DefaultAuthenticateScheme = Esquema;
                autenticacion.DefaultChallengeScheme = Esquema;
                autenticacion.DefaultForbidScheme = Esquema;
            })
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ManejadorSesionClarisa>(Esquema, null);
        // Toda ruta sin marca exige sesión; las anónimas se declaran una a una con AllowAnonymous (RutasClarisa.Anonimas).
        servicios.AddAuthorization(autorizacion => autorizacion.FallbackPolicy = new AuthorizationPolicyBuilder(Esquema)
            .RequireAuthenticatedUser().Build());
        return servicios;
    }
}
