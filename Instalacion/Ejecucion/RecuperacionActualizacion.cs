using Clarisa.Instalacion.Contratos;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Pasos.Servidor;

namespace Clarisa.Instalacion.Ejecucion;

internal static class RecuperacionActualizacion
{
    public static void Completar(ContextoEjecucion contexto)
    {
        if (contexto.Plan.Modo != ModoInstalacion.Servidor || !contexto.Hechos.ContainsKey("preparar-actualizacion")) return;
        var rutas = contexto.RutasServidor;
        if (PuntoActualizacion.Leer(rutas) is { Completada: false, ProgramasRevertidos: false, LimpiezaPendiente: false } punto)
            (punto with { Completada = true }).Guardar(rutas);
    }
    public static async Task<ResultadoInstalacion> AnteFalloAsync(ContextoEjecucion contexto, ResultadoInstalacion resultado)
    {
        if (contexto.Plan.Modo != ModoInstalacion.Servidor) return resultado;
        if (!contexto.Hechos.ContainsKey("preparar-actualizacion"))
            return contexto.Hechos.ContainsKey("api-detenida-para-copia")
                ? resultado with { Indicaciones = [.. resultado.Indicaciones, await ReanudarApiAsync(contexto)] }
                : resultado;
        try
        {
            var rutas = contexto.RutasServidor;
            var punto = PuntoActualizacion.Leer(rutas);
            if (punto is null || punto.Completada || punto.ProgramasRevertidos) return resultado;
            var indicaciones = resultado.Indicaciones.ToList();
            if (punto.MigracionIniciada)
            {
                if (ServiciosPropios.EnEjecucion(ServiciosPropios.ConsultarPropio(contexto, RutasServidor.ServicioApi)))
                    await contexto.Sistema.Servicios.DetenerAsync(RutasServidor.ServicioApi, TimeSpan.FromSeconds(60), CancellationToken.None);
                if (punto.HaciaDelante && punto.LimpiezaPendiente)
                    indicaciones.Add("El esquema quedó confirmado, pero la credencial temporal de migración pudo seguir válida. La API permanece detenida: " +
                                     "reintenta con el mismo paquete para repetir la verificación y completar la limpieza, sin restaurar datos antiguos.");
                else if (punto.HaciaDelante)
                    indicaciones.Add("La migración y su limpieza quedaron confirmadas. La API permanece detenida: corrige el paso pendiente y reintenta con el mismo paquete para completar hacia delante, sin restaurar datos antiguos.");
                else indicaciones.Add("La migración pudo modificar la base. No se restauran automáticamente programas antiguos sobre esos datos. "
                    + "Usa el mantenimiento conservado en " + punto.Directorio(rutas) + " para preparar y activar la copia " + punto.Copia + " en una base nueva.");
            }
            else
            {
                foreach (var nombre in new[] { RutasServidor.ServicioApi, RutasServidor.ServicioMotor })
                    if (ServiciosPropios.EnEjecucion(ServiciosPropios.ConsultarPropio(contexto, nombre)))
                        await contexto.Sistema.Servicios.DetenerAsync(nombre, TimeSpan.FromSeconds(60), CancellationToken.None);
                await punto.RevertirProgramasAsync(rutas, contexto.Paquete.Del(contexto.Plan.Modo), CancellationToken.None);
                (punto with { ProgramasRevertidos = true }).Guardar(rutas);
                indicaciones.Add("Se repusieron y verificaron los programas y ajustes anteriores; los servicios quedan detenidos para revisión. La base no llegó a migrarse.");
            }
            return resultado with { Indicaciones = indicaciones };
        }
        catch (Exception error)
        {
            return resultado with { Estado = EstadoInstalacion.Fallida, Indicaciones = [.. resultado.Indicaciones,
                contexto.Filtro.Limpiar("La recuperación necesita revisión: " + error.Message + " No borres los datos ni el punto de actualización.")] };
        }
    }

    // La copia falló antes de sustituir nada: los programas y datos son los que la API usaba, así que vuelve a iniciarse. Con un punto
    // pendiente de un intento interrumpido los programas pueden estar mezclados y la API se deja detenida.
    private static async Task<string> ReanudarApiAsync(ContextoEjecucion contexto)
    {
        const string detenida = "La API quedó detenida para preparar la copia; no se sustituyeron programas. Corrige la causa y reintenta " +
                                "la instalación para comprobar y restablecer el servicio.";
        try
        {
            if (PuntoActualizacion.Leer(contexto.RutasServidor) is { Completada: false, ProgramasRevertidos: false }) return detenida;
            await contexto.Sistema.Servicios.IniciarAsync(RutasServidor.ServicioApi, TimeSpan.FromSeconds(90), CancellationToken.None);
            return "No se sustituyeron programas ni datos: la API anterior volvió a iniciarse. Corrige la causa y reintenta la actualización.";
        }
        catch (Exception error) when (error is FalloInstalacionException or IOException or UnauthorizedAccessException
                                          or System.ComponentModel.Win32Exception or InvalidOperationException or TimeoutException)
        {
            return detenida + " No se pudo volver a iniciarla: " + contexto.Filtro.Limpiar(error.Message);
        }
    }
}
