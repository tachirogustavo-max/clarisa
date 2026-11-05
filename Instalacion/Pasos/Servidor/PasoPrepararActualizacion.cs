using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Retirada;

namespace Clarisa.Instalacion.Pasos.Servidor;

internal sealed class PasoPrepararActualizacion : IPasoInstalacion
{
    private const string CopiaRenovada = ":copia-renovada";

    public string Id => "preparar-actualizacion";
    public string Nombre => "Protección de la instalación anterior";
    public string Descripcion => "Antes de sustituir programas, conserva una copia cifrada verificada y los programas y ajustes anteriores.";
    public bool Critico => true;
    public bool Privilegiado => true;

    // Todo lo que puede impedir la copia se decide aquí, antes de que AplicarAsync detenga la API.
    public async Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        if (contexto.Estado.Retirada is { } retirada) return Reinstalacion(contexto, retirada);
        if (!InstalacionAnterior(contexto))
            return Comprobacion.Satisfecha("Primera instalación: este equipo no tiene el servicio del servidor ni un servicio aprobado antes.");
        var fuente = await ReparacionEntorno.DecidirAsync(contexto, ct);
        if (fuente.Configuracion is null)
            throw new FalloInstalacionException("Hay una instalación anterior sin configuración verificable; no se detuvo la API ni se sustituyó nada.");
        var rutas = contexto.RutasServidor;
        var punto = PuntoActualizacion.Leer(rutas);
        var pendiente = punto is { Completada: false, ProgramasRevertidos: false } ? punto : null;
        if (VersionMotor.Cambio(contexto, pendiente) is { } motor) throw new FalloInstalacionException(motor);
        if (pendiente is null) return Comprobacion.NoSatisfecha("Debe prepararse la recuperación antes de sustituir la instalación anterior.");
        if (pendiente.MigracionIniciada && !pendiente.HaciaDelante) throw new FalloInstalacionException("Una actualización anterior alcanzó la migración y no terminó. Revisa su copia y recupere en una base nueva; no se repite a ciegas.");
        if (pendiente.VersionDestino != contexto.Paquete.Version || pendiente.HuellaDestino != contexto.Paquete.HuellaContenido)
            throw new FalloInstalacionException("Hay una actualización pendiente de otro paquete. Recupérela antes de continuar.");
        // Interrumpida antes de migrar (corte, cierre forzado): los servicios con inicio automático pudieron atender cambios después
        // de la copia, así que se renueva antes de seguir. Tras la migración ya no se vuelve a esos datos y la copia no se repite.
        if (!pendiente.MigracionIniciada && !contexto.Hechos.ContainsKey(Id + CopiaRenovada))
            return Comprobacion.NoSatisfecha("Se reanuda una actualización interrumpida antes de migrar: se renueva la copia previa.");
        await pendiente.VerificarAsync(rutas, ct);
        contexto.Hechos[Id] = pendiente.Directorio(rutas);
        return Comprobacion.Satisfecha("Copia cifrada y programas anteriores verificados. Punto: " + pendiente.Directorio(rutas));
    }

    public async Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var rutas = contexto.RutasServidor;
        var api = ServiciosPropios.ConsultarPropio(contexto, RutasServidor.ServicioApi);
        if (ServiciosPropios.EnEjecucion(api))
        {
            await contexto.Sistema.Servicios.DetenerAsync(RutasServidor.ServicioApi, TimeSpan.FromSeconds(60), ct);
            contexto.Hechos["api-detenida-para-copia"] = "1";
        }
        var motor = ServiciosPropios.ConsultarPropio(contexto, RutasServidor.ServicioMotor)
            ?? throw new FalloInstalacionException("Falta el motor de la instalación anterior; no se sustituyeron programas.");
        if (!ServiciosPropios.EnEjecucion(motor))
            await contexto.Sistema.Servicios.IniciarAsync(RutasServidor.ServicioMotor, TimeSpan.FromSeconds(60), ct);
        var id = contexto.Plan.Id;
        if (PuntoActualizacion.Leer(rutas) is { Completada: false, ProgramasRevertidos: false, MigracionIniciada: false } pendiente)
        {
            await RenovarAsync(contexto, pendiente, id, ct);
            return;
        }
        var copia = await CopiaPrevia.TomarAsync(contexto, id, rutas.MantenimientoInstalado, ct);
        var archivos = await PuntoActualizacion.ConservarAsync(rutas, id, ct);
        var punto = new PuntoActualizacion(id, rutas.Programa, rutas.Datos, contexto.Paquete.Version, contexto.Paquete.HuellaContenido, id,
            copia.Archivo, copia.Bytes, copia.Sha256, archivos);
        await punto.VerificarAsync(rutas, ct);
        punto.Guardar(rutas);
        contexto.Hechos[Id + CopiaRenovada] = "1";
        contexto.Hechos[Id] = punto.Directorio(rutas);
        contexto.Anotar(Id, "Recuperación preparada en " + punto.Directorio(rutas));
    }

    // Los programas instalados pueden ser ya una mezcla de versiones; la copia se toma con el mantenimiento conservado en el punto,
    // que corresponde a los datos sin migrar. Los programas anteriores del punto no se sustituyen.
    private async Task RenovarAsync(ContextoEjecucion contexto, PuntoActualizacion pendiente, Guid id, CancellationToken ct)
    {
        var rutas = contexto.RutasServidor;
        await pendiente.VerificarAsync(rutas, ct);
        // El punto de una versión anterior al cambio de nombre conserva el mantenimiento con su nombre antiguo.
        var relativa = new[] { "programa/servidor/Clarisa.Mantenimiento.exe", "programa/servidor/" + ProgramasAnteriores.Mantenimiento }
                           .FirstOrDefault(r => pendiente.Archivos.Any(a => a.Ruta == r))
            ?? throw new FalloInstalacionException("El punto pendiente no conserva el mantenimiento anterior; no se continúa.");
        var copia = await CopiaPrevia.TomarAsync(contexto, id, RutasSeguras.Dentro(pendiente.Directorio(rutas), relativa), ct);
        var renovado = pendiente with { CopiaId = id, Copia = copia.Archivo, BytesCopia = copia.Bytes, Sha256Copia = copia.Sha256 };
        await renovado.VerificarAsync(rutas, ct);
        renovado.Guardar(rutas);
        contexto.Hechos[Id + CopiaRenovada] = "1";
        contexto.Anotar(Id, $"Copia previa renovada al reanudar el punto {pendiente.Directorio(rutas)}; la anterior se conserva.");
    }

    // Tras una retirada los programas ya no están y los datos quedaron en la versión retirada: solo se reinstala ese mismo paquete. Otro
    // paquete migraría o cambiaría el motor sin una copia previa tomada con su mantenimiento, así que se rechaza antes de tocar nada.
    private static Comprobacion Reinstalacion(ContextoEjecucion contexto, RetiradaConservada retirada)
    {
        var paquete = contexto.Paquete;
        if (retirada.Version == paquete.Version && string.Equals(retirada.HuellaManifiesto, paquete.HuellaContenido, StringComparison.OrdinalIgnoreCase))
            return Comprobacion.Satisfecha($"Reinstalación de la versión {retirada.Version} sobre los datos conservados en la retirada; se " +
                                           "reutilizan y comprueban sin tratarlos como una base nueva." +
                                           (retirada.Copia is null ? "" : $" Copia de retirada: {retirada.Copia}."));
        var conservado = Path.Combine(ReferenciaRetirada.Carpeta(contexto.Plan.DirectorioRegistros), "mantenimiento");
        throw new FalloInstalacionException($"Los datos de este servidor se conservaron al retirar la versión {retirada.Version}. Este paquete " +
            $"({paquete.Version}) no se instala sobre ellos: primero reinstala la versión {retirada.Version} y después actualiza con el flujo " +
            "normal, que toma su copia previa; o restaura la copia de retirada " + (retirada.Copia ?? "(sin copia registrada)") +
            $" con el mantenimiento conservado en {conservado} en una instalación nueva. No se detuvo ni se cambió nada.");
    }

    // No basta la verificación final aprobada: con red Pública nunca se aprueba y la API ya puede estar en servicio con datos.
    internal static bool InstalacionAnterior(ContextoEjecucion contexto) =>
        contexto.Estado.PasosAprobados.ContainsKey("servicio-api") || contexto.Estado.PasosAprobados.ContainsKey("verificacion-api")
        || contexto.Sistema.Servicios.Consultar(RutasServidor.ServicioApi) is { } api && ServiciosPropios.Ajeno(contexto, api) is null;
}
