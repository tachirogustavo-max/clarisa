using System.Text.RegularExpressions;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Pasos.Servidor;

// Sin un archivo de recuperación exportado y releído la recuperación no está preparada aunque exista la clave en el servidor.
internal sealed partial class PasoClaveRespaldos : IPasoInstalacion
{
    private const string Identificador = "clave-respaldos";
    private static readonly TimeSpan Limite = TimeSpan.FromMinutes(2);

    public string Id => Identificador;
    public string Nombre => "Clave de las copias de seguridad";
    public string Descripcion => "Crea la clave de respaldos, la exporta al destino elegido fuera del servidor y relee el archivo exportado.";
    public bool Critico => true;
    public bool Privilegiado => true;

    public async Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var id = await IdPreparadaAsync(contexto, ct);
        if (id is null) return Comprobacion.NoSatisfecha("El servidor no tiene clave de respaldos.");
        var destino = contexto.Servidor.DestinoClaveRecuperacion;
        var exportada = IdExportada(destino);
        if (exportada == id)
            return await CoincideConServidorAsync(contexto, id.Value, destino, ct)
                ? Comprobacion.Satisfecha($"Clave {id} preparada y exportada a {destino}; el archivo se releyó y coincide con la del servidor.")
                : Comprobacion.NoSatisfecha($"El archivo {destino} declara la clave {id} pero su código no coincide con la del servidor.");
        if (exportada is not null) return Comprobacion.NoSatisfecha($"El archivo {destino} corresponde a otra clave.");
        if (!contexto.Estado.EsPropio(TipoArtefacto.Archivo, "exportacion:" + id))
            return Comprobacion.NoSatisfecha($"La clave {id} existe pero no se ha exportado al destino elegido.");
        // El operador indicó un archivo nuevo: se escribe y se comprueba, en lugar de darlo por hecho sin crearlo.
        return Path.GetDirectoryName(destino) is { } carpeta && Directory.Exists(carpeta)
            ? Comprobacion.NoSatisfecha($"La clave {id} ya se exportó antes; se escribirá y comprobará en {destino} otra copia del mismo archivo de recuperación.")
            : Comprobacion.Satisfecha($"Clave {id} exportada y verificada en una ejecución anterior. El destino {destino} no está conectado: no se " +
                                      "creó un archivo nuevo; conserva el archivo exportado antes.");
    }

    public async Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var rutas = contexto.RutasServidor;
        var destino = contexto.Servidor.DestinoClaveRecuperacion;
        if (File.Exists(destino)) throw new FalloInstalacionException("El archivo de recuperación indicado ya existe; no se sustituye. Elige otro nombre.");
        var existe = await IdPreparadaAsync(contexto, ct) is not null;
        IReadOnlyList<string> argumentos = existe
            ? ["exportar-clave", "--configuracion", rutas.ArchivoConfiguracion, "--destino", destino]
            : ["crear-clave", "--configuracion", rutas.ArchivoConfiguracion, "--exportar", destino];
        var resultado = await contexto.Sistema.Procesos.EjecutarAsync(new SolicitudProceso(rutas.EjecutableMantenimiento, argumentos, Limite), ct);
        contexto.Anotar(Id, $"Código {resultado.Codigo}. Salida: {resultado.Salida}");
        if (resultado.Codigo != 0 || resultado.VencioLimite)
            throw new FalloInstalacionException($"El mantenimiento no preparó/exportó la clave (código {resultado.Codigo}). La recuperación NO está lista.");
        if (!existe) contexto.Propio(TipoArtefacto.Archivo, rutas.ArchivoClaveRespaldos, conservar: true);
        if (await IdPreparadaAsync(contexto, ct) is { } id && IdExportada(destino) == id && await CoincideConServidorAsync(contexto, id, destino, ct))
            contexto.Propio(TipoArtefacto.Archivo, "exportacion:" + id, conservar: true);
    }

    // Con una clave ya preparada, importar-clave descifra el archivo, valida su suma de control y compara el material en tiempo
    // constante sin modificar nada. Solo se invoca cuando el servidor tiene clave: así nunca instala una distinta.
    private static async Task<bool> CoincideConServidorAsync(ContextoEjecucion contexto, Guid id, string archivo, CancellationToken ct)
    {
        var rutas = contexto.RutasServidor;
        var resultado = await contexto.Sistema.Procesos.EjecutarAsync(new SolicitudProceso(rutas.EjecutableMantenimiento,
            ["importar-clave", "--configuracion", rutas.ArchivoConfiguracion, "--origen", archivo], Limite), ct);
        contexto.Anotar(Identificador, $"Verificación del archivo exportado: código {resultado.Codigo}.");
        return !resultado.VencioLimite && resultado.Codigo == 0
               && resultado.Salida.Contains($"Clave {id} preparada en este servidor.", StringComparison.Ordinal);
    }

    private static async Task<Guid?> IdPreparadaAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var rutas = contexto.RutasServidor;
        if (!File.Exists(rutas.ArchivoClaveRespaldos)) return null;
        var resultado = await contexto.Sistema.Procesos.EjecutarAsync(new SolicitudProceso(rutas.EjecutableMantenimiento,
            ["estado", "--configuracion", rutas.ArchivoConfiguracion], Limite), ct);
        if (resultado.Codigo != 0) throw new FalloInstalacionException($"El mantenimiento no informó su estado (código {resultado.Codigo}).");
        var coincidencia = Preparada().Match(resultado.Salida);
        return coincidencia.Success && Guid.TryParse(coincidencia.Groups[1].Value, out var id) ? id : null;
    }

    // Solo se leen las líneas de cabecera; el código de la clave no se interpreta ni se registra.
    internal static Guid? IdExportada(string archivo)
    {
        var info = new FileInfo(archivo);
        if (!info.Exists || info.Length is 0 or > 8 * 1024) return null;
        var lineas = File.ReadLines(info.FullName).Take(20).ToList();
        if (!lineas.Contains("FORMATO: ESCOLAR-RECUPERACION-1", StringComparer.Ordinal)) return null;
        if (!lineas.Any(l => l.StartsWith("CODIGO: ", StringComparison.Ordinal) && l.Length > 40)) return null;
        var id = lineas.FirstOrDefault(l => l.StartsWith("ID: ", StringComparison.Ordinal));
        return id is not null && Guid.TryParse(id[4..], out var valor) ? valor : null;
    }

    [GeneratedRegex(@"Clave de respaldos: preparada, Id ([0-9a-fA-F-]{36})")]
    private static partial Regex Preparada();
}
