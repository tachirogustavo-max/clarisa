using System.ComponentModel;
using Clarisa.Instalacion.Contratos;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Retirada;

// Retirada con la misma disciplina que la instalación: cada paso comprueba su postcondición, solo actúa si falta y vuelve a comprobar.
// La cancelación se observa entre pasos. Ante un fallo los siguientes no se ejecutan y la entrada de Windows queda para reintentar.
public sealed class MotorRetirada
{
    private readonly SistemaInstalacion sistema;
    private readonly RevisorRetirada revisor;

    internal MotorRetirada(SistemaInstalacion sistema, string registrosEstacion)
    {
        this.sistema = sistema;
        revisor = new RevisorRetirada(sistema, registrosEstacion);
    }

    public async Task<RevisionRetirada> RevisarAsync(SolicitudRetirada solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        return (await revisor.RevisarAsync(solicitud, ct)).Publica;
    }

    public async Task<ResultadoRetirada> EjecutarAsync(RevisionRetirada revision, bool confirmado, IProgress<AvanceInstalacion>? avance, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(revision);
        if (!confirmado) return Rechazo(revision, "La retirada requiere la confirmación explícita. No se hizo ningún cambio.");
        var actual = await revisor.RevisarAsync(new SolicitudRetirada(revision.Referencia), ct);
        if (!actual.Publica.Valida || actual.Referencia is null || actual.Estado is null || actual.Publica.Id != revision.Id
            || !actual.Publica.Pasos.Select(p => p.Id).SequenceEqual(revision.Pasos.Select(p => p.Id)))
            return Rechazo(revision, "La instalación cambió desde la revisión: " +
                                     string.Join(" ", actual.Publica.Problemas.Select(p => p.Mensaje).DefaultIfEmpty("vuelve a revisarla.")) + " No se hizo ningún cambio.");
        FileStream bloqueo;
        try
        {
            bloqueo = new FileStream(Path.Combine(actual.Registros, "ejecucion.bloqueo"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            return Rechazo(revision, "Hay otra ejecución del instalador en curso. No se hizo ningún cambio.");
        }
        using var turno = bloqueo;
        var pasos = CatalogoRetirada.Para(actual.Referencia, actual.Carpeta);
        var plan = new PlanInstalacion(Guid.NewGuid(), actual.Referencia.Modo, actual.Referencia.Reconstruir(actual.Carpeta),
            revision.Pasos, [], actual.Registros);
        var filtro = new FiltroSecretos();
        using var registro = RegistroInstalacion.Abrir(actual.Registros, plan.Id, filtro, sistema.Reloj, "retirada");
        var contexto = new ContextoEjecucion(plan, new OperadorSinInteraccion(), sistema, registro, filtro, actual.Estado);
        registro.Escribir("inicio", "detalle", $"Retirada de {plan.Modo} confirmada; {pasos.Count} pasos.");
        var resultados = new List<ResultadoPaso>();
        var detenido = false;
        for (var indice = 0; indice < pasos.Count; indice++)
        {
            var paso = pasos[indice];
            if (detenido || ct.IsCancellationRequested)
            {
                var estado = detenido ? EstadoPaso.NoEjecutado : EstadoPaso.Cancelado;
                resultados.Add(Cerrar(registro, paso, estado, detenido ? "No se ejecutó porque una etapa anterior no terminó." : "Cancelado antes de empezar este paso."));
                detenido = true;
                continue;
            }
            avance?.Report(new AvanceInstalacion(indice + 1, pasos.Count, paso.Id, paso.Nombre, EstadoPaso.EnCurso, paso.Descripcion));
            var (resultado, detalle, yaEstaba) = await EjecutarPasoAsync(contexto, paso, ct);
            resultados.Add(Cerrar(registro, paso, resultado, filtro.Limpiar(detalle), yaEstaba));
            avance?.Report(new AvanceInstalacion(indice + 1, pasos.Count, paso.Id, paso.Nombre, resultado, filtro.Limpiar(detalle)));
            if (resultado is EstadoPaso.Fallido or EstadoPaso.Cancelado || (paso.Critico && resultado == EstadoPaso.NoComprobado)) detenido = true;
        }
        var global = Global(resultados);
        var indicaciones = Indicaciones(global, actual.Publica);
        registro.Escribir("resumen", global.ToString(), indicaciones[0]);
        return new ResultadoRetirada(plan.Id, plan.Modo, global, resultados, registro.Ruta, indicaciones);
    }

    private static async Task<(EstadoPaso, string, bool)> EjecutarPasoAsync(ContextoEjecucion contexto, IPasoInstalacion paso, CancellationToken ct)
    {
        try
        {
            var previa = await paso.ComprobarAsync(contexto, ct);
            switch (previa.Tipo)
            {
                case TipoComprobacion.Satisfecha: return (EstadoPaso.Aprobado, "Ya estaba retirado: " + previa.Detalle, true);
                case TipoComprobacion.Omitida: return (EstadoPaso.Omitido, previa.Detalle, false);
                case TipoComprobacion.Indeterminada: return (EstadoPaso.NoComprobado, previa.Detalle, false);
            }
            contexto.Anotar(paso.Id, "Antes: " + previa.Detalle);
            await paso.AplicarAsync(contexto, ct);
            var posterior = await paso.ComprobarAsync(contexto, ct);
            return posterior.Tipo switch
            {
                TipoComprobacion.Satisfecha => (EstadoPaso.Aprobado, posterior.Detalle, false),
                TipoComprobacion.Omitida => (EstadoPaso.Omitido, posterior.Detalle, false),
                TipoComprobacion.Indeterminada => (EstadoPaso.NoComprobado, posterior.Detalle, false),
                _ => (EstadoPaso.Fallido, "La acción terminó pero su comprobación no se cumple: " + posterior.Detalle, false)
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return (EstadoPaso.Cancelado, "Cancelado durante una espera segura; revisa el registro y reintenta la retirada.", false);
        }
        catch (Exception error) when (error is FalloInstalacionException or IOException or UnauthorizedAccessException or Win32Exception
                                          or InvalidDataException or InvalidOperationException or TimeoutException
                                          or MySqlConnector.MySqlException or System.Runtime.InteropServices.COMException)
        {
            return (EstadoPaso.Fallido, error switch
            {
                FalloInstalacionException => error.Message,
                IOException => "Un archivo está en uso o no se pudo modificar: " + error.Message + " Cierra la aplicación y reintenta.",
                UnauthorizedAccessException => "Permiso denegado: " + error.Message,
                _ => $"{error.GetType().Name}: {error.Message}"
            }, false);
        }
    }

    private static ResultadoPaso Cerrar(RegistroInstalacion registro, IPasoInstalacion paso, EstadoPaso estado, string detalle, bool yaEstaba = false)
    {
        registro.Escribir(paso.Id, estado.ToString(), detalle);
        return new ResultadoPaso(paso.Id, paso.Nombre, paso.Critico, estado, detalle, yaEstaba);
    }

    private static EstadoRetirada Global(IReadOnlyList<ResultadoPaso> pasos)
    {
        if (pasos.Any(p => p.Estado == EstadoPaso.Fallido)) return EstadoRetirada.Fallida;
        if (pasos.Any(p => p.Estado == EstadoPaso.Cancelado)) return EstadoRetirada.Cancelada;
        return pasos.Where(p => p.Critico).All(p => p.Estado == EstadoPaso.Aprobado) ? EstadoRetirada.Completada : EstadoRetirada.Incompleta;
    }

    private static List<string> Indicaciones(EstadoRetirada estado, RevisionRetirada revision)
    {
        var conservado = "Se conservan: " + string.Join("; ", revision.SeConserva.Select(e => e.Ubicacion is null ? e.Descripcion : $"{e.Descripcion} ({e.Ubicacion})")) + ".";
        return estado switch
        {
            EstadoRetirada.Completada => ["Retirada completada y comprobada.", conservado],
            EstadoRetirada.Cancelada => ["Retirada detenida en una frontera segura. La entrada de Aplicaciones instaladas sigue disponible para reintentar.", conservado],
            _ => ["La retirada NO terminó. Corrige la causa indicada y reintenta desde Aplicaciones instaladas: lo ya retirado se comprueba sin repetirse.", conservado]
        };
    }

    private static ResultadoRetirada Rechazo(RevisionRetirada revision, string motivo) => new(Guid.NewGuid(), revision.Pasos.Count == 0 ? null : revision.Modo,
        EstadoRetirada.Rechazada, revision.Pasos.Select(p => new ResultadoPaso(p.Id, p.Nombre, p.Critico, EstadoPaso.NoEjecutado, motivo)).ToList(), null, [motivo]);

    private sealed class OperadorSinInteraccion : IOperadorInstalacion
    {
        public Task<CredencialOperador?> SolicitarClaveOperadorMotorAsync(CancellationToken ct) => Task.FromResult<CredencialOperador?>(null);
        public Task<CredencialOperador?> SolicitarAccesoPrimeraCuentaAsync(string usuario, CancellationToken ct) => Task.FromResult<CredencialOperador?>(null);
        public Task<bool> ConfirmarAsync(string mensaje, CancellationToken ct) => Task.FromResult(false);
    }
}
