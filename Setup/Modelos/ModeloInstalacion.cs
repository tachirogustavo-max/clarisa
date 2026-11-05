using System.Collections.ObjectModel;
using Clarisa.Instalacion;
using Clarisa.Instalacion.Contratos;
using Clarisa.Setup.Servicios;

namespace Clarisa.Setup.Modelos;
public sealed class ModeloInstalacion : ModeloObservable, IModeloProgreso
{
    private readonly CancellationTokenSource cierre = new();
    private int pagina;
    private string mensaje = "";
    private bool confirmado;
    private bool ejecutando;
    private bool finalizado;
    private bool mostrarDetalles;
    private PasoInstalacionVisible? pasoActual;
    public bool MostrarDetalles { get => mostrarDetalles; set => Cambiar(ref mostrarDetalles, value); }
    public PasoInstalacionVisible? PasoActual { get => pasoActual; private set => Cambiar(ref pasoActual, value); }
    private PlanInstalacion? plan;
    public ConfiguracionInstalacion Configuracion { get; } = new();
    public ObservableCollection<PasoInstalacionVisible> Pasos { get; } = [];
    public IReadOnlyList<LineaResumen> Resumen { get; private set; } = [];
    public IReadOnlyList<ProblemaSolicitud> Problemas { get; private set; } = [];
    public ResultadoInstalacion? Resultado { get; private set; }
    public int Pagina { get => pagina; private set { Cambiar(ref pagina, value); Actualizar(); } }
    public bool EquipoVisible => Pagina == 0;
    public bool ConfiguracionVisible => Pagina == 1;
    public bool RevisionVisible => Pagina == 2;
    public bool ProgresoVisible => Pagina == 3;
    public bool MostrarContinuar => Pagina < 3;
    public bool MostrarRegistro => Finalizado && !string.IsNullOrWhiteSpace(ArchivoRegistro);
    public bool Confirmado { get => confirmado; set { Cambiar(ref confirmado, value); Actualizar(); } }
    public bool Ejecutando { get => ejecutando; private set { Cambiar(ref ejecutando, value); Actualizar(); } }
    public bool Finalizado { get => finalizado; private set { Cambiar(ref finalizado, value); Actualizar(); } }
    public bool Terminada => Resultado?.Terminada == true;
    public bool PuedeVolver => Pagina is 1 or 2 && !Ejecutando;
    public bool PuedeContinuar => !Ejecutando && Pagina < 3 && (Pagina != 2 || Confirmado);
    public string TextoContinuar => Pagina == 2 ? "Instalar y comprobar" : "Continuar";
    public string Mensaje { get => mensaje; private set => Cambiar(ref mensaje, value); }
    public string TituloProgreso => !Finalizado ? "Preparando tu equipo" : Terminada ? "Equipo preparado" : "La instalación necesita atención";
    public string DescripcionProgreso => "Cada paso se marca cuando termina su comprobación.";
    public string? ArchivoRegistro => Resultado?.ArchivoRegistro;
    public IReadOnlyList<string> Indicaciones => Resultado?.Indicaciones ?? [];
    public DatosEstaciones? Estaciones => Resultado?.Estaciones;

    public void Volver()
    {
        if (!PuedeVolver) return;
        Pagina--;
        Mensaje = "";
    }
    public async Task ContinuarAsync(IOperadorInstalacion operador)
    {
        if (!PuedeContinuar) return;
        Mensaje = "";
        if (Pagina == 0) { Configuracion.SugerirRed(); Pagina = 1; return; }
        if (Pagina == 1) { Preparar(); return; }
        if (plan is null || !Confirmado) return;
        Ejecutando = true;
        Pagina = 3;
        try
        {
            var progreso = new Progress<AvanceInstalacion>(avance =>
            {
                if (Finalizado) return;
                var paso = Pasos.FirstOrDefault(p => p.Id == avance.PasoId);
                paso?.Actualizar(avance.Estado, avance.Detalle);
                PasoActual = paso;
            });
            var motor = ComposicionInstalacion.CrearMotor(IdentidadPaquete.Exigir(plan.Solicitud.OrigenPaquete));
            Resultado = await Task.Run(() => motor.EjecutarAsync(plan, true, operador, progreso, cierre.Token));
            foreach (var paso in Resultado.Pasos) Pasos.FirstOrDefault(p => p.Id == paso.Id)?.Actualizar(paso.Estado, paso.Detalle);
            PasoActual = Pasos.FirstOrDefault(p => p.Estado is "Fallido" or "Sin comprobar") ?? Pasos.LastOrDefault(p => p.Estado != "No ejecutado") ?? PasoActual;
            Mensaje = Resultado.Terminada ? "Se completaron las comprobaciones obligatorias." : "Revisa los pasos y el registro antes de usar este equipo.";
        }
        catch (OperationCanceledException) when (cierre.IsCancellationRequested)
        { Mensaje = "La instalación se detuvo. Revisa el registro antes de volver a intentarlo."; }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { Mensaje = "No se pudo completar la instalación. Revisa los permisos, los archivos y el registro. No se presenta como terminada."; }
        finally { Ejecutando = false; Finalizado = true; Actualizar(); }
    }
    private void Preparar()
    {
        try
        {
            IdentidadPaquete.Exigir(Configuracion.OrigenPaquete);
            var respuesta = ComposicionInstalacion.CrearPlanificador().Planificar(Configuracion.CrearSolicitud());
            Problemas = respuesta.Problemas;
            Notificar(nameof(Problemas));
            if (respuesta.Plan is not { } valido || !respuesta.Valido)
            { Mensaje = "Revisa los datos señalados antes de continuar."; return; }
            plan = valido;
            Resumen = valido.Resumen;
            Notificar(nameof(Resumen));
            Pasos.Clear();
            foreach (var paso in valido.Pasos) Pasos.Add(new(paso.Id, paso.Nombre, paso.Descripcion));
            if (Pasos.LastOrDefault() is { } ultimo) ultimo.Ultimo = true;
            Confirmado = false;
            Pagina = 2;
        }
        catch (ArgumentException error) { Mensaje = error.Message; }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or InvalidOperationException)
        { Mensaje = error.Message; }
    }
    public void SolicitarCancelacion()
    {
        if (!Ejecutando) return;
        cierre.Cancel();
        Mensaje = "Se detendrá al terminar el paso actual. No cierres el equipo.";
    }
    private void Actualizar()
    {
        foreach (var nombre in new[] { nameof(EquipoVisible), nameof(ConfiguracionVisible), nameof(RevisionVisible), nameof(ProgresoVisible),
            nameof(PuedeVolver), nameof(PuedeContinuar), nameof(TextoContinuar), nameof(TituloProgreso), nameof(Terminada), nameof(ArchivoRegistro), nameof(Indicaciones), nameof(Estaciones), nameof(MostrarContinuar), nameof(MostrarRegistro) }) Notificar(nombre);
    }
#if DEBUG
    public void VistaDiseno(int paso)
    {
        Configuracion.DireccionEscucha = "192.168.1.10";
        Configuracion.EstablecerRedes([new("Red del liceo", "192.168.1.10")]);
        Configuracion.DireccionServidor = "https://equipo-servidor:5189";
        Configuracion.ExportarClave = "Unidad externa — elegir destino";
        Resumen = [new("Equipo", "Servidor"), new("Conexión", "https://equipo-servidor:5189"), new("Información", "Se conservará separada del programa")];
        Pasos.Add(new("requisitos", "Comprobar el equipo", "Compatibilidad, permisos y espacio disponible."));
        Pasos.Add(new("programa", "Preparar los programas", "Verificar los archivos antes y después de copiarlos."));
        Pasos.Add(new("datos", "Preparar la información", "Crear el almacén y comprobar sus reglas."));
        Pasos.Add(new("seguridad", "Configurar el acceso", "Conexión segura y primera cuenta de dirección."));
        Pasos.Add(new("respaldo", "Preparar la recuperación", "Exportar la clave y comprobar su preparación."));
        Pasos[^1].Ultimo = true;
        if (paso == 3)
        {
            Ejecutando = true;
            Pasos[0].Actualizar(EstadoPaso.Aprobado, "Equipo compatible y espacio comprobado.");
            Pasos[1].Actualizar(EstadoPaso.Aprobado, "Los archivos coinciden con el paquete.");
            Pasos[2].Actualizar(EstadoPaso.EnCurso, "Preparando la información del liceo.");
            PasoActual = Pasos[2];
        }
        Pagina = paso;
    }
#endif
}
