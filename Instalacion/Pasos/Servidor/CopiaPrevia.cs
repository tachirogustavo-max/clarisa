using System.Text.Json;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Registro;

namespace Clarisa.Instalacion.Pasos.Servidor;

internal sealed record CopiaConfirmada(Guid Id, string Archivo, long Bytes, string Sha256);

// Copia cifrada de la base y su verificación completa con el mantenimiento indicado, sobre la fuente de configuración fijada.
internal static class CopiaPrevia
{
    private static readonly TimeSpan Limite = TimeSpan.FromMinutes(10);

    public static async Task<CopiaConfirmada> TomarAsync(ContextoEjecucion contexto, Guid id, string mantenimiento, CancellationToken ct)
    {
        var rutas = contexto.RutasServidor;
        var respuesta = await contexto.Sistema.Procesos.EjecutarAsync(ConfiguracionEfectiva.Mantenimiento(contexto,
            "respaldo-local", Limite, "--operacion", id.ToString("D")) with { Ejecutable = mantenimiento }, ct);
        if (respuesta.Codigo != 0 || respuesta.VencioLimite)
            throw new FalloInstalacionException("No se confirmó la copia previa con el mantenimiento anterior. No se sustituyeron programas ni se migraron datos.");
        var linea = respuesta.Salida.Split('\n').Select(l => l.Trim()).SingleOrDefault(l => l.StartsWith("COPIA_VERIFICADA ", StringComparison.Ordinal))
            ?? throw new FalloInstalacionException("El mantenimiento no confirmó los datos de la copia previa.");
        CopiaConfirmada copia;
        try { copia = JsonSerializer.Deserialize<CopiaConfirmada>(linea[17..]) ?? throw new JsonException(); }
        catch (JsonException error) { throw new FalloInstalacionException("La confirmación de copia no es válida.", error); }
        var archivo = Path.Combine(rutas.Respaldos, "copias", id.ToString("N") + ".liceo-respaldo");
        if (copia.Id != id || !string.Equals(copia.Archivo, archivo, StringComparison.OrdinalIgnoreCase) || copia.Bytes <= 0)
            throw new FalloInstalacionException("La copia confirmada no corresponde a este intento.");
        await PuntoActualizacion.ExigirHuellaAsync(archivo, copia.Bytes, copia.Sha256, ct);
        var verificacion = await contexto.Sistema.Procesos.EjecutarAsync(ConfiguracionEfectiva.Mantenimiento(contexto,
            "verificar", Limite, "--archivo", archivo) with { Ejecutable = mantenimiento }, ct);
        if (verificacion.Codigo != 0 || verificacion.VencioLimite || !verificacion.Salida.Contains("La copia se descifró y todas sus tablas coinciden con su manifiesto.", StringComparison.Ordinal))
            throw new FalloInstalacionException("La copia previa no pasó su verificación completa. No se actualizó la instalación.");
        return copia with { Archivo = archivo };
    }
}
