using System.ComponentModel;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Retirada;

namespace Clarisa.Instalacion.Pasos.Comunes;

// Tras una actualización retira los archivos de programa de versiones anteriores que este paquete ya no trae, por ejemplo los
// Escolar.* anteriores al cambio de nombre. Solo los que figuran en el inventario de retirada con una huella que instaló este
// programa, dentro de la carpeta propia. No es crítico: un archivo que no se pueda borrar queda anotado y lo retira la desinstalación.
internal sealed class PasoProgramasAnteriores : IPasoInstalacion
{
    private const string Intentado = ":intentado";

    public string Id => "programas-anteriores";
    public string Nombre => "Programas de versiones anteriores";
    public string Descripcion => "Retira los archivos de programa de versiones anteriores que este paquete ya no incluye.";
    public bool Critico => false;
    public bool Privilegiado => true;

    public async Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        List<string> restos;
        try { restos = await RestosAsync(contexto, ct); }
        catch (Exception error) when (error is FalloInstalacionException or IOException or UnauthorizedAccessException)
        {
            return Comprobacion.Indeterminada("No se pudo revisar el inventario de versiones anteriores: " + contexto.Filtro.Limpiar(error.Message));
        }
        if (restos.Count == 0) return Comprobacion.Satisfecha("No quedan archivos de programa de versiones anteriores.");
        var detalle = $"Quedan {restos.Count} archivos de versiones anteriores, por ejemplo {restos[0]}.";
        return contexto.Hechos.TryGetValue(Id + Intentado, out var motivo)
            ? Comprobacion.Indeterminada(detalle + " " + motivo + " La desinstalación los retira igualmente.")
            : Comprobacion.NoSatisfecha(detalle);
    }

    public async Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        contexto.Hechos[Id + Intentado] = "";
        foreach (var archivo in await RestosAsync(contexto, ct))
        {
            try
            {
                RutasSeguras.ExigirSinPuntosReparacion(archivo);
                File.Delete(archivo);
            }
            catch (Exception error) when (error is FalloInstalacionException or IOException or UnauthorizedAccessException or Win32Exception)
            {
                contexto.Hechos[Id + Intentado] = "No se pudo borrar " + archivo + ": " + contexto.Filtro.Limpiar(error.Message);
            }
        }
    }

    private static async Task<List<string>> RestosAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var programa = contexto.Plan.Solicitud.DirectorioPrograma;
        var carpeta = ReferenciaRetirada.Carpeta(contexto.Plan.DirectorioRegistros);
        var archivo = ReferenciaRetirada.Archivo(carpeta);
        if (!contexto.Estado.EsPropio(TipoArtefacto.Directorio, programa) || !contexto.Estado.EsPropio(TipoArtefacto.Herramienta, carpeta)
            || !File.Exists(archivo)) return [];
        var actuales = contexto.Paquete.Del(contexto.Plan.Modo).Select(a => a.Ruta).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var restos = new List<string>();
        foreach (var entrada in ReferenciaRetirada.Leer(archivo).Inventario.Where(e => !actuales.Contains(e.Ruta)))
        {
            var ruta = RutasSeguras.Dentro(programa, entrada.Ruta);
            if (await PasoRegistroRetirada.HuellaAsync(ruta, ct) is var (bytes, huella)
                && entrada.Versiones.Any(v => v.Bytes == bytes && string.Equals(v.Sha256, huella, StringComparison.OrdinalIgnoreCase)))
                restos.Add(ruta);
        }
        return restos;
    }
}
