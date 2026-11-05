using Clarisa.Instalacion.Contratos;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Retirada;

// Retira del estado las aprobaciones de lo eliminado y deja la marca de datos retirados: una reinstalación posterior no los tratará como
// base nueva ni como una instalación en servicio, y solo aceptará el mismo paquete (PasoPrepararActualizacion).
internal sealed class PasoEstadoRetirada(ReferenciaRetirada referencia) : IPasoInstalacion
{
    private static readonly string[] Retirados =
    [
        "requisitos", "puertos", "paquete", "preparar-actualizacion", "detener-servicios", "programa", "motor-servicio", "servicio-api",
        "red-local", "verificacion-api", "reinicio-servicio", "conexion-servidor", "acceso-directo", "registro-retirada"
    ];

    public string Id => "estado-retirada";
    public string Nombre => "Estado de la retirada";
    public string Descripcion => "Anota la retirada y la versión de los datos conservados para una reinstalación segura.";
    public bool Critico => true;
    public bool Privilegiado => true;

    public Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct) => Task.FromResult(
        contexto.Estado.Retirada is { Completa: true } && !Retirados.Any(contexto.Estado.PasosAprobados.ContainsKey)
            ? Comprobacion.Satisfecha($"Retirada anotada; los datos conservados corresponden a la versión {referencia.Version}.")
            : Comprobacion.NoSatisfecha("Falta anotar la retirada en el estado."));

    public Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var estado = contexto.Estado;
        foreach (var id in Retirados) estado.PasosAprobados.Remove(id);
        estado.Retirada = (estado.Retirada ?? new RetiradaConservada(contexto.Sistema.Reloj(), referencia.Version, referencia.HuellaManifiesto,
            null, null, 0, null, null, false)) with { Completa = true };
        estado.Guardar(contexto.Plan.DirectorioRegistros);
        return Task.CompletedTask;
    }
}

// Último paso: la entrada de Aplicaciones instaladas solo desaparece cuando todo lo crítico anterior quedó aprobado. La herramienta y la
// referencia se conservan siempre en su carpeta protegida.
internal sealed class PasoRetirarRegistroWindows(string referencia) : IPasoInstalacion
{
    public string Id => "registro-windows";
    public string Nombre => "Quitar de Aplicaciones instaladas";
    public string Descripcion => "Elimina la entrada propia de Aplicaciones instaladas; la herramienta protegida y los registros se conservan.";
    public bool Critico => true;
    public bool Privilegiado => true;

    public Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var entrada = contexto.Sistema.Aplicaciones.Leer(RegistroRetirada.Clave(contexto.Plan.Modo));
        if (entrada is null) return Task.FromResult(Comprobacion.Satisfecha("La entrada ya no figura en Aplicaciones instaladas."));
        return Task.FromResult(Propia(contexto, entrada)
            ? Comprobacion.NoSatisfecha("La entrada de Aplicaciones instaladas sigue presente.")
            : Comprobacion.Indeterminada("La entrada de Aplicaciones instaladas fue modificada y apunta a otra herramienta: se conserva para revisión."));
    }

    public Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var clave = RegistroRetirada.Clave(contexto.Plan.Modo);
        if (contexto.Sistema.Aplicaciones.Leer(clave) is { } entrada && Propia(contexto, entrada))
        {
            contexto.Sistema.Aplicaciones.Eliminar(clave);
            contexto.Estado.OlvidarArtefacto(TipoArtefacto.RegistroAplicacion, clave);
            contexto.Estado.Guardar(contexto.Plan.DirectorioRegistros);
        }
        return Task.CompletedTask;
    }

    private bool Propia(ContextoEjecucion contexto, EntradaAplicacion entrada) =>
        contexto.Estado.EsPropio(TipoArtefacto.RegistroAplicacion, entrada.Clave)
        && entrada.Desinstalar.Contains($"\"{referencia}\"", StringComparison.OrdinalIgnoreCase);
}

internal static class CatalogoRetirada
{
    public static IReadOnlyList<IPasoInstalacion> Para(ReferenciaRetirada referencia, string carpeta) => referencia.Modo switch
    {
        ModoInstalacion.Servidor =>
        [
            new PasoCopiaRetirada(referencia, carpeta),
            new PasoRetirarServicio(RutasServidor.ServicioApi, Pasos.Servidor.PasoServicioApi.Definicion),
            new PasoRetirarServicio(RutasServidor.ServicioMotor, Pasos.Servidor.PasoMotorServicio.Definicion),
            new PasoRetirarReglaFirewall(),
            new PasoRetirarProgramas(referencia),
            new PasoEstadoRetirada(referencia),
            new PasoRetirarRegistroWindows(ReferenciaRetirada.Archivo(carpeta))
        ],
        _ =>
        [
            new PasoRetirarAccesos(referencia),
            new PasoConservarConfianza(),
            new PasoRetirarProgramas(referencia),
            new PasoEstadoRetirada(referencia),
            new PasoRetirarRegistroWindows(ReferenciaRetirada.Archivo(carpeta))
        ]
    };
}
