using System.ComponentModel;
using System.Security.Cryptography;
using Clarisa.Instalacion.Contratos;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Ejecucion;

public sealed class MotorInstalacion
{
    private static readonly IReadOnlyList<ReglaAcceso> AccesoRegistros =
        [new(IdentidadServicio.Administradores, DerechoAcceso.Control), new(IdentidadServicio.Sistema, DerechoAcceso.Control)];

    private readonly SistemaInstalacion sistema;
    private readonly Func<ModoInstalacion, IReadOnlyList<IPasoInstalacion>> catalogo;
    private readonly string registrosEstacion;
    private readonly string? huellaManifiesto;

    internal MotorInstalacion(SistemaInstalacion sistema, Func<ModoInstalacion, IReadOnlyList<IPasoInstalacion>> catalogo,
        string registrosEstacion, string? huellaManifiesto = null)
    {
        this.sistema = sistema;
        this.catalogo = catalogo;
        this.registrosEstacion = registrosEstacion;
        this.huellaManifiesto = huellaManifiesto;
    }

    public async Task<ResultadoInstalacion> EjecutarAsync(PlanInstalacion plan, bool confirmado, IOperadorInstalacion operador,
        IProgress<AvanceInstalacion>? avance, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(operador);
        var pasos = catalogo(plan.Modo);
        if (!confirmado) return Rechazo(plan, "La instalación requiere la confirmación explícita del resumen. No se hizo ningún cambio.");
        if (MotivoPlanInvalido(plan, pasos) is { } invalido) return Rechazo(plan, invalido);

        var filtro = new FiltroSecretos();
        EstadoPersistido estado;
        RegistroInstalacion registro;
        FileStream? bloqueo = null;
        try
        {
            if (PrepararRegistros(plan) is { } motivo) return Rechazo(plan, motivo);
            bloqueo = new FileStream(Path.Combine(plan.DirectorioRegistros, "ejecucion.bloqueo"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            estado = EstadoPersistido.Cargar(plan.DirectorioRegistros, plan.Modo.ToString());
            registro = RegistroInstalacion.Abrir(plan.DirectorioRegistros, plan.Id, filtro, sistema.Reloj);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            bloqueo?.Dispose();
            return Rechazo(plan, "No se pudo preparar el registro de la instalación: " + error.Message + " No se instaló nada.");
        }

        using var turno = bloqueo;
        var resultados = new List<ResultadoPaso>();
        IPasoInstalacion? pasoActual = null;
        ContextoEjecucion? contexto = null;
        try
        {
            contexto = new ContextoEjecucion(plan, operador, sistema, registro, filtro, estado, huellaManifiesto);
            registro.Escribir("inicio", "detalle", $"Modo {plan.Modo}; {pasos.Count} pasos confirmados por el operador.");
            var detenido = false;
            for (var indice = 0; indice < pasos.Count; indice++)
            {
                var paso = pasos[indice];
                pasoActual = paso;
                if (detenido)
                {
                    resultados.Add(Cerrar(registro, paso, EstadoPaso.NoEjecutado, "No se ejecutó porque una etapa anterior no terminó."));
                    continue;
                }
                if (ct.IsCancellationRequested)
                {
                    resultados.Add(Cerrar(registro, paso, EstadoPaso.Cancelado, "Cancelado por el operador antes de empezar este paso."));
                    detenido = true;
                    continue;
                }
                avance?.Report(new AvanceInstalacion(indice + 1, pasos.Count, paso.Id, paso.Nombre, EstadoPaso.EnCurso, paso.Descripcion));
                var resultado = await EjecutarPasoAsync(contexto, paso, ct);
                resultados.Add(Cerrar(registro, paso, resultado.Estado, resultado.Detalle, resultado.YaEstaba));
                avance?.Report(new AvanceInstalacion(indice + 1, pasos.Count, paso.Id, paso.Nombre, resultado.Estado, filtro.Limpiar(resultado.Detalle)));
                if (resultado.Estado == EstadoPaso.Aprobado)
                {
                    estado.PasosAprobados[paso.Id] = sistema.Reloj();
                    estado.Guardar(plan.DirectorioRegistros);
                }
                if (resultado.Estado is EstadoPaso.Fallido or EstadoPaso.Cancelado || (paso.Critico && resultado.Estado == EstadoPaso.NoComprobado))
                    detenido = true;
            }
            var global = Global(resultados);
            registro.Escribir("resumen", global.ToString(), Indicaciones(global).FirstOrDefault() ?? "");
            var respuesta = new ResultadoInstalacion(plan.Id, plan.Modo, global, resultados.Select(r => r with { Detalle = filtro.Limpiar(r.Detalle) }).ToList(),
                registro.Ruta, Indicaciones(global)) { Estaciones = DatosParaEstaciones(contexto) };
            if (respuesta.Estaciones is { } datos)
                registro.Escribir("estaciones", "detalle", $"Dirección {datos.Direccion}; certificado {datos.Certificado}; huella {datos.Huella}.");
            if (respuesta.Terminada || ServicioRestablecido(respuesta)) RecuperacionActualizacion.Completar(contexto);
            else respuesta = await RecuperacionActualizacion.AnteFalloAsync(contexto, respuesta);
            return respuesta;
        }
        catch (Exception error)
        {
            // El motivo queda en el registro aunque el fallo ocurra fuera de un paso.
            try { registro.Escribir("interrumpida", EstadoInstalacion.Fallida.ToString(), Describir(error) + Environment.NewLine + error); }
            catch (Exception escritura) when (escritura is IOException or ObjectDisposedException) { }
            var fallo = ResumenInterrumpido.Crear(plan, pasos, resultados.Select(r => r with { Detalle = filtro.Limpiar(r.Detalle) }).ToArray(), pasoActual, registro.Ruta,
                filtro.Limpiar(Describir(error)));
            return contexto is null ? fallo : await RecuperacionActualizacion.AnteFalloAsync(contexto, fallo);
        }
        finally
        {
            registro.Dispose();
            contexto?.DescartarCredenciales();
        }
    }

    private static async Task<(EstadoPaso Estado, string Detalle, bool YaEstaba)> EjecutarPasoAsync(ContextoEjecucion contexto,
        IPasoInstalacion paso, CancellationToken ct)
    {
        try
        {
            var previa = await paso.ComprobarAsync(contexto, ct);
            switch (previa.Tipo)
            {
                case TipoComprobacion.Satisfecha: return (EstadoPaso.Aprobado, "Ya estaba correcto: " + previa.Detalle, true);
                case TipoComprobacion.Omitida: return (EstadoPaso.Omitido, previa.Detalle, false);
            }
            contexto.Anotar(paso.Id, "Antes: " + previa.Detalle);
            await paso.AplicarAsync(contexto, ct);
            var posterior = await paso.ComprobarAsync(contexto, ct);
            return posterior.Tipo switch
            {
                TipoComprobacion.Satisfecha => (EstadoPaso.Aprobado, posterior.Detalle, false),
                TipoComprobacion.Indeterminada => (EstadoPaso.NoComprobado, posterior.Detalle, false),
                TipoComprobacion.Omitida => (EstadoPaso.Omitido, posterior.Detalle, false),
                _ => (EstadoPaso.Fallido, "La acción terminó pero su comprobación no se cumple: " + posterior.Detalle, false)
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return (EstadoPaso.Cancelado, "Cancelado por el operador durante una espera segura; revisa el registro antes de reintentar.", false);
        }
        catch (Exception error)
        {
            // Cualquier error, previsto o no, detiene la instalación como Fallida: así el registro y el resumen se cierran sin dar nada por bueno.
            return (EstadoPaso.Fallido, Describir(error), false);
        }
    }

    private static string Describir(Exception error) => error switch
    {
        FalloInstalacionException => error.Message,
        UnauthorizedAccessException => "Permiso denegado: " + error.Message,
        MySqlConnector.MySqlException sql => $"El motor de datos rechazó la operación (código {sql.ErrorCode}).",
        OperationCanceledException => "La operación superó su tiempo límite.",
        HttpRequestException http => "No se pudo conectar con el servidor: " + (http.InnerException?.Message ?? http.Message),
        IOException or Win32Exception or CryptographicException or InvalidDataException or TimeoutException => $"{error.GetType().Name}: {error.Message}",
        _ => $"Error inesperado del instalador ({error.GetType().Name}): {error.Message}"
    };

    // El plan es un registro público: se vuelve a validar por si se construyó a mano o cambió el equipo desde la planificación.
    private string? MotivoPlanInvalido(PlanInstalacion plan, IReadOnlyList<IPasoInstalacion> pasos)
    {
        if (plan.Solicitud is null || plan.Solicitud.Modo != plan.Modo || !CoincideConCatalogo(plan, pasos))
            return "El plan no corresponde a esta versión del instalador. Vuelve a planificar.";
        var problemas = new Planificacion.ValidadorSolicitud(sistema.Equipo).Validar(plan.Solicitud);
        if (problemas.Count > 0) return "Los datos de la instalación ya no son válidos: " + string.Join(" ", problemas.Select(p => p.Mensaje));
        var previstos = Planificacion.PlanificadorInstalacion.Registros(plan.Solicitud, registrosEstacion);
        return string.Equals(Path.GetFullPath(plan.DirectorioRegistros), Path.GetFullPath(previstos), StringComparison.OrdinalIgnoreCase)
            ? null
            : "La carpeta de registros del plan no es la prevista. Vuelve a planificar.";
    }

    // El estado previo decide qué es propio; solo se confía en él si su carpeta ya estaba protegida antes de esta ejecución.
    private string? PrepararRegistros(PlanInstalacion plan)
    {
        var directorio = plan.DirectorioRegistros;
        RutasSeguras.ExigirSinPuntosReparacion(directorio);
        var previa = sistema.Permisos.Leer(directorio);
        if (File.Exists(EstadoPersistido.Ruta(directorio)) && previa?.Coincide(AccesoRegistros) != true)
            return $"Hay un estado de instalación anterior en {directorio} sin la protección esperada; no se confía en él. Revisa esa carpeta " +
                   "con el responsable técnico antes de continuar. No se hizo ningún cambio.";
        // Cada carpeta que falta hasta los registros (incluida la de datos) nace protegida; la primera que ya existe debe ser segura
        // antes de crear nada dentro, porque quien la posea podría apartar lo creado.
        var faltantes = new Stack<string>();
        var actual = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directorio));
        while (!Directory.Exists(actual) && Path.GetDirectoryName(actual) is { } superior)
        {
            faltantes.Push(actual);
            actual = superior;
        }
        if (sistema.Equipo.AncestroInseguro(faltantes.TryPeek(out var primera) ? primera : actual) is { } inseguro)
            return "La carpeta de registros no es segura: " + inseguro + " No se hizo ningún cambio.";
        while (faltantes.TryPop(out var carpeta))
            sistema.Permisos.Restringir(carpeta, AccesoRegistros);
        sistema.Permisos.Restringir(directorio, AccesoRegistros);
        return sistema.Permisos.Leer(directorio)?.Coincide(AccesoRegistros) == true
            ? null
            : $"No se pudo comprobar la protección de la carpeta de registros {directorio}.";
    }

    private static ResultadoPaso Cerrar(RegistroInstalacion registro, IPasoInstalacion paso, EstadoPaso estado, string detalle,
        bool yaEstaba = false)
    {
        registro.Escribir(paso.Id, estado.ToString(), detalle);
        return new ResultadoPaso(paso.Id, paso.Nombre, paso.Critico, estado, detalle, yaEstaba);
    }

    internal static EstadoInstalacion Global(IReadOnlyList<ResultadoPaso> pasos)
    {
        if (pasos.Any(p => p.Estado == EstadoPaso.Fallido)) return EstadoInstalacion.Fallida;
        if (pasos.Any(p => p.Estado == EstadoPaso.Cancelado)) return EstadoInstalacion.Cancelada;
        var criticos = pasos.Where(p => p.Critico).ToList();
        return criticos.All(p => p.Estado == EstadoPaso.Aprobado) ? EstadoInstalacion.Completada : EstadoInstalacion.Incompleta;
    }

    private static DatosEstaciones? DatosParaEstaciones(ContextoEjecucion contexto)
    {
        if (contexto.Plan.Modo != ModoInstalacion.Servidor || !contexto.Hechos.TryGetValue("huella-certificado", out var huella)) return null;
        var rutas = contexto.RutasServidor;
        return new DatosEstaciones(rutas.DireccionPublica.AbsoluteUri, rutas.ArchivoCertificadoPublico, huella);
    }

    private static IReadOnlyList<string> Indicaciones(EstadoInstalacion estado) => estado switch
    {
        EstadoInstalacion.Completada => ["Todas las acciones obligatorias se ejecutaron y comprobaron."],
        EstadoInstalacion.Incompleta => ["Hay acciones obligatorias sin comprobar: la instalación NO está lista. Corrige la causa indicada y " +
                                         "vuelve a ejecutar el instalador; los pasos ya correctos se comprueban sin repetirse."],
        EstadoInstalacion.Fallida => ["Una acción falló y las siguientes no se ejecutaron. Revisa el registro, corrige la causa y vuelve a " +
                                      "ejecutar: los pasos ya correctos se comprueban sin repetirse. No se borraron datos, claves ni copias."],
        EstadoInstalacion.Cancelada => ["Instalación cancelada en una frontera segura. Vuelve a ejecutarla para continuar; lo hecho se " +
                                        "comprueba sin repetirse."],
        _ => ["No se hizo ningún cambio."]
    };

    // Incompleta sin fallos con el servicio ya aprobado (por ejemplo, red Pública): la actualización terminó y la API queda en servicio;
    // solo faltan comprobaciones posteriores que no justifican detenerla.
    private static bool ServicioRestablecido(ResultadoInstalacion resultado) => resultado.Estado == EstadoInstalacion.Incompleta
        && resultado.Pasos.Any(p => p.Id == "servicio-api" && p.Estado == EstadoPaso.Aprobado);

    private static bool CoincideConCatalogo(PlanInstalacion plan, IReadOnlyList<IPasoInstalacion> pasos) =>
        plan.Pasos.Select(p => p.Id).SequenceEqual(pasos.Select(p => p.Id), StringComparer.Ordinal);

    private static ResultadoInstalacion Rechazo(PlanInstalacion plan, string motivo) =>
        new(plan.Id, plan.Modo, EstadoInstalacion.Rechazada, plan.Pasos.Select(p =>
            new ResultadoPaso(p.Id, p.Nombre, p.Critico, EstadoPaso.NoEjecutado, motivo)).ToList(), null, [motivo]);
}
