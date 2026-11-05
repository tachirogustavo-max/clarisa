using System.Security.Cryptography.X509Certificates;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Pasos.Servidor;

// La API solo se considera operativa si responde por HTTPS con el certificado de esta instalación y la primera cuenta inicia y cierra
// sesión de verdad (lo que exige la base). «Servicio en ejecución» no basta.
internal sealed class PasoVerificacionApi : IPasoInstalacion
{
    public string Id => "verificacion-api";
    public string Nombre => "Servidor, base y primera cuenta";
    public string Descripcion => "Conecta por la dirección de las estaciones, valida el certificado e inicia sesión con la primera cuenta.";
    public bool Critico => true;
    public bool Privilegiado => false;
    public const int Intentos = 3;

    public async Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        if (contexto.Hechos.TryGetValue(Id, out var hecho)) return Comprobacion.Satisfecha(hecho);
        if (await VerificacionHttps.AnonimaAsync(contexto, ct) is { } problema) return problema;
        if (contexto.Hechos.TryGetValue(Id + ":pendiente", out var pendiente)) return Comprobacion.Indeterminada(pendiente);
        return Comprobacion.NoSatisfecha("Falta comprobar el inicio de sesión real.");
    }

    // Un rechazo de acceso (contraseña mal escrita) no es un fallo del servidor: se ofrece reintentar y, si no, el paso queda sin comprobar
    // con la API en servicio. Cualquier otra respuesta o un certificado incorrecto siguen siendo un fallo.
    public async Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var usuario = contexto.Servidor.UsuarioPrimeraCuenta;
        for (var intento = 1; ; intento++)
        {
            using var credencial = contexto.TomarCredencialPrimeraCuenta()
                                   ?? await contexto.Operador.SolicitarAccesoPrimeraCuentaAsync(usuario, ct);
            if (credencial is null)
            {
                contexto.Hechos[Id + ":pendiente"] = "El operador no indicó la contraseña: el inicio de sesión real queda sin comprobar.";
                return;
            }
            contexto.Filtro.Registrar(credencial.ClaveTexto());
            using var certificado = PasoCertificado.Cargar(contexto.RutasServidor);
            using var publico = X509CertificateLoader.LoadCertificate(certificado.RawData);
            var respuesta = await contexto.Sistema.Cliente.IniciarYCerrarSesionAsync(contexto.RutasServidor.DireccionPublica, publico, credencial, ct);
            if (respuesta.Estado == System.Net.HttpStatusCode.OK)
            {
                contexto.Hechos[Id] = $"HTTPS en {contexto.RutasServidor.DireccionPublica.Authority} con el certificado propio; {respuesta.Detalle}";
                return;
            }
            if (!respuesta.RechazoAcceso) throw new FalloInstalacionException("La primera cuenta no pudo iniciar sesión: " + respuesta.Detalle);
            var motivo = $"El servidor respondió y no aceptó el acceso de {credencial.Usuario}: {respuesta.Detalle}";
            contexto.Anotar(Id, $"Intento {intento}: {motivo}");
            if (intento < Intentos && await contexto.Operador.ConfirmarAsync(motivo + " La API sigue en servicio y no se cambió ningún dato. " +
                                                                             "¿Quieres escribir la contraseña de nuevo?", ct))
                continue;
            contexto.Hechos[Id + ":pendiente"] = motivo + " El inicio de sesión real queda sin comprobar y la API sigue en servicio: vuelve a " +
                                                 "ejecutar el instalador con la contraseña correcta.";
            return;
        }
    }
}

internal sealed class PasoReinicioServicio : IPasoInstalacion
{
    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(90);

    public string Id => "reinicio-servicio";
    public string Nombre => "Detención e inicio del servicio";
    public string Descripcion => "Detiene e inicia de nuevo el servidor y comprueba otra vez que responde. El reinicio del equipo se ensaya aparte.";
    public bool Critico => true;
    public bool Privilegiado => true;

    public Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct) => Task.FromResult(
        contexto.Hechos.TryGetValue(Id, out var hecho)
            ? Comprobacion.Satisfecha(hecho)
            : Comprobacion.NoSatisfecha("El ciclo de detención e inicio se ensaya en cada instalación."));

    public async Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var servicios = contexto.Sistema.Servicios;
        await servicios.DetenerAsync(RutasServidor.ServicioApi, Limite, ct);
        if (servicios.Consultar(RutasServidor.ServicioApi)?.Estado != EstadoServicio.Detenido)
            throw new FalloInstalacionException("El servicio no quedó detenido.");
        await servicios.IniciarAsync(RutasServidor.ServicioApi, Limite, ct);
        var diferencia = ComparadorServicio.Diferencia(servicios.Consultar(RutasServidor.ServicioApi), PasoServicioApi.Definicion(contexto.RutasServidor));
        if (diferencia is not null) throw new FalloInstalacionException(diferencia);
        if (await VerificacionHttps.AnonimaAsync(contexto, ct) is { } problema)
            throw new FalloInstalacionException("Tras reiniciar el servicio: " + problema.Detalle);
        contexto.Hechos[Id] = "El servicio se detuvo, volvió a iniciar con inicio automático y la API respondió por HTTPS.";
    }
}

internal static class VerificacionHttps
{
    // Reintenta durante el arranque: el servicio puede estar «en ejecución» antes de aceptar conexiones.
    public static async Task<Comprobacion?> AnonimaAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        using var certificado = PasoCertificado.Cargar(contexto.RutasServidor);
        using var publico = X509CertificateLoader.LoadCertificate(certificado.RawData);
        var direccion = contexto.RutasServidor.DireccionPublica;
        string ultimo = "";
        for (var intento = 0; intento < 10; intento++)
        {
            try
            {
                var respuesta = await contexto.Sistema.Cliente.ConsultarSesionAnonimaAsync(direccion, publico, ct);
                if (RespuestaEsperada.ApiActiva(respuesta.Estado)) return null;
                return Comprobacion.NoSatisfecha("La API respondió de forma inesperada: " + respuesta.Detalle);
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                ultimo = error.InnerException?.Message ?? error.Message;
            }
            await contexto.Sistema.Esperar(TimeSpan.FromSeconds(intento < 3 ? 1 : 3), ct);
        }
        return Comprobacion.NoSatisfecha($"La API no respondió en {direccion.Authority}: {ultimo}");
    }
}
