using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Clarisa.Desktop.Services;
using Clarisa.Desktop.Presentation.Commands;

namespace Clarisa.Desktop.Presentation.Models;
public class ModeloPlanilla : INotifyPropertyChanged
{
    private readonly IClientePlanillas? cliente;
    private readonly ContextoPlanilla? contexto;
    private readonly CancellationTokenSource cierre = new();
    private string busqueda = "";
    private int paginaActual = 1;
    private int tamanoPagina = 25;
    private FilaNota? filaSeleccionada;
    private OpcionFiltroNotas filtro = new(FiltroNotas.Todas, "Todas las notas");
    public event PropertyChangedEventHandler? PropertyChanged;
    public ModeloPlanilla()
    {
        PaginaAnterior = new Comando(_ => CambiarPagina(PaginaActual - 1), _ => PaginaActual > 1 && !Guardando);
        PaginaSiguiente = new Comando(_ => CambiarPagina(PaginaActual + 1), _ => PaginaActual < TotalPaginas && !Guardando);
    }

    public ModeloPlanilla(IClientePlanillas cliente, ContextoPlanilla contexto, IEnumerable<FilaNota> filas, bool cargaPermitida, IClienteLecturas? lecturas = null, IClienteExoneraciones? exoneraciones = null) : this()
    {
        this.cliente = cliente;
        this.contexto = contexto;
        CargaPermitida = cargaPermitida;
        EstablecerFilas(filas);
        if (exoneraciones is not null) Exoneracion = new(exoneraciones, contexto, () => PuedeEditar && Cambios == 0 && !RequiereRevision, (nota, motivo) =>
        {
            var fila = Filas.Single(f => f.InscripcionId == nota.InscripcionId);
            fila.Confirmar(nota.Valor, nota.Version, nota.Letra, nota.Tipo, motivo);
            fila.Restaurar();
            Actualizar();
        }, () => { RequiereRevision = true; Actualizar(); });
        Comparador = new ComparadorNotas(lecturas, contexto, () => Filas, (nuevas, carga) =>
        {
            EstablecerFilas(nuevas);
            CargaPermitida = carga;
            RequiereRevision = false;
            Actualizar();
            ActualizarPagina();
        });
    }

    public ComparadorNotas? Comparador { get; }
    public ModeloExoneracion? Exoneracion { get; }
    public FilaNota? FilaSeleccionada { get => filaSeleccionada; set { filaSeleccionada = value; Notificar(); } }
    public bool Administracion => Exoneracion is not null;
    public ObservableCollection<FilaNota> Filas { get; } = [];
    private IEnumerable<FilaNota> Filtradas => Filas.Where(f => (f.Estudiante.Contains(busqueda, StringComparison.CurrentCultureIgnoreCase) || f.Cedula.Contains(busqueda, StringComparison.OrdinalIgnoreCase)) && (filtro.Valor switch
    {
        FiltroNotas.Pendientes => !f.TieneError && f.Pendiente,
        FiltroNotas.Cambios => f.Cambiada,
        FiltroNotas.Errores => f.TieneError,
        _ => true
    }));
    public IEnumerable<FilaNota> FilasVisibles => Filtradas.Skip((PaginaActual - 1) * TamanoPagina).Take(TamanoPagina);

    public string Busqueda
    {
        get => busqueda;
        set
        {
            busqueda = value ?? "";
            paginaActual = 1;
            Notificar();
            ActualizarPagina();
        }
    }

    public OpcionFiltroNotas FiltroSeleccionado
    {
        get => filtro;
        set
        {
            filtro = value ?? OpcionesFiltro[0];
            paginaActual = 1;
            Notificar();
            ActualizarPagina();
        }
    }

    public IReadOnlyList<OpcionFiltroNotas> OpcionesFiltro { get; } = [new(FiltroNotas.Todas, "Todas las notas"), new(FiltroNotas.Pendientes, "Pendientes"), new(FiltroNotas.Cambios, "Sin guardar"), new(FiltroNotas.Errores, "Con errores")];
    public IReadOnlyList<int> TamaniosPagina { get; } = [10, 25, 50, 100];

    public int TamanoPagina
    {
        get => tamanoPagina;
        set
        {
            if (!TamaniosPagina.Contains(value))
                return;
            tamanoPagina = value;
            paginaActual = 1;
            Notificar();
            ActualizarPagina();
        }
    }

    public int PaginaActual => Math.Min(paginaActual, TotalPaginas);
    public int TotalFiltradas => Filtradas.Count();
    public int TotalPaginas => Math.Max(1, (TotalFiltradas + TamanoPagina - 1) / TamanoPagina);
    public string TextoPagina => TotalFiltradas == 0 ? "Sin resultados" : "Página " + PaginaActual + " de " + TotalPaginas + " · " + TotalFiltradas + " estudiantes";
    public Comando PaginaAnterior { get; }
    public Comando PaginaSiguiente { get; }
    public string Materia { get; set; } = "Calificaciones";
    public string Seccion { get; set; } = "";
    public string Lapso { get; set; } = "";
    public bool CargaPermitida { get; private set; }
    public bool Guardando { get; private set; }
    public bool RequiereRevision { get; private set; }
    public bool Confirmado { get; private set; }
    public string Mensaje { get; private set; } = "";
    public int Cambios => Filas.Count(f => f.Cambiada);
    public int Errores => Filas.Count(f => f.TieneError);
    public int Pendientes => Filas.Count(f => !f.TieneError && f.Pendiente);
    public bool MostrarNumeros => Filas.Count == 0 || Filas.Any(f => f.Modalidad is 2 or 4);
    public bool MostrarLetras => Filas.Any(f => f.Modalidad is 3 or 4);
    public bool PuedeEditar => CargaPermitida && !Guardando;
    public bool PuedeGuardar => PuedeEditar && Cambios > 0 && Errores == 0 && !RequiereRevision && cliente is not null;
    public string Resumen => Guardando ? "Confirmando con el servidor…" : Errores == 1 ? "1 nota por revisar" : Errores > 1 ? Errores + " notas por revisar" : Cambios == 1 ? "1 cambio sin guardar" : Cambios > 1 ? Cambios + " cambios sin guardar" : "Sin cambios pendientes";
    public string TextoPendientes => Pendientes == 1 ? "1 pendiente" : Pendientes + " pendientes";
    public string Instruccion => !CargaPermitida ? "Solo lectura. La carga debe estar abierta y la materia formar parte del plan para guardar."
        : MostrarLetras ? "Selecciona una letra de la escala del año. Si la materia usa nota y letra, completa ambas. EX solo lo modifica dirección."
        : "Escribe la nota o deja el campo pendiente. Los cambios se envían juntos al guardar.";
    public string EstadoCarga => CargaPermitida ? "Carga habilitada" : "Carga cerrada · Solo lectura";
    public string TituloSalida => Guardando ? "Guardado en curso" : Cambios > 0 ? "Cambios sin guardar" : "Cerrar planilla";
    public string AvisoSalida => Guardando ? "Espera la respuesta del servidor antes de salir. Así podrás comprobar qué notas quedaron guardadas." : Cambios > 0 ? "Si sales, perderás la copia local de lo que estás editando. Las notas que ya estén guardadas no se eliminan." : "La planilla ya no tiene cambios pendientes.";

    protected void EstablecerFilas(IEnumerable<FilaNota> filas)
    {
        foreach (var fila in Filas)
            fila.PropertyChanged -= CambioFila;
        Filas.Clear();
        foreach (var fila in filas)
        {
            Filas.Add(fila);
            fila.PropertyChanged += CambioFila;
        }

        Actualizar();
    }

    public async Task GuardarAsync()
    {
        if (!PuedeGuardar || cierre.IsCancellationRequested)
            return;
        var enviadas = Filas.Where(f => f.Cambiada).Select(f => new NotaParaGuardar(f.InscripcionId, f.Valor, f.Version)
            { Letra = f.Letra, IncluyeInasistencias = !f.Exonerada, Inasistencias = f.Inasistencias }).ToList();
        if (enviadas.Count > 200)
        {
            Mensaje = "Hay más de 200 cambios. No se enviaron notas; reduce el lote antes de guardar.";
            Actualizar();
            return;
        }

        Guardando = true;
        Confirmado = false;
        Mensaje = "";
        Actualizar();
        try
        {
            var respuesta = await cliente!.GuardarAsync(contexto!, enviadas, cierre.Token);
            cierre.Token.ThrowIfCancellationRequested();
            if (respuesta.Fallo != FalloGuardado.Ninguno)
            {
                if (respuesta.Fallo == FalloGuardado.VentanaCerrada)
                    CargaPermitida = false;
                if (respuesta.Fallo is FalloGuardado.VersionObsoleta or FalloGuardado.Conexion)
                    RequiereRevision = true;
                Mensaje = respuesta.Mensaje ?? "No se pudo confirmar el guardado. Tu edición se conserva.";
                return;
            }

            if (!ValidacionConfirmacionNotas.EsValida(enviadas, respuesta.Notas))
            {
                RequiereRevision = true;
                Mensaje = "La confirmación está incompleta. Tu edición se conserva para revisarla.";
                return;
            }

            var confirmadas = respuesta.Notas.ToDictionary(n => n.InscripcionId);
            foreach (var enviada in enviadas)
            {
                var confirmada = confirmadas[enviada.InscripcionId];
                Filas.Single(f => f.InscripcionId == enviada.InscripcionId).Confirmar(confirmada.Valor, confirmada.Version, confirmada.Letra, confirmada.Tipo,
                    inasistencias: confirmada.Inasistencias);
            }

            Mensaje = "Cambios confirmados por el servidor.";
            Confirmado = true;
        }
        catch (OperationCanceledException)when (cierre.IsCancellationRequested)
        {
        }
        finally
        {
            Guardando = false;
            Actualizar();
            ActualizarPagina();
        }
    }

    public void Cancelar()
    {
        cierre.Cancel();
        Exoneracion?.Cancelar();
        Comparador?.Ocultar();
    }

    public void MostrarFila(FilaNota fila)
    {
        busqueda = "";
        filtro = OpcionesFiltro[0];
        paginaActual = Math.Max(1, Filas.IndexOf(fila) / TamanoPagina + 1);
        Notificar(nameof(Busqueda));
        Notificar(nameof(FiltroSeleccionado));
        ActualizarPagina();
    }

    private void CambiarPagina(int pagina)
    {
        paginaActual = Math.Clamp(pagina, 1, TotalPaginas);
        ActualizarPagina();
    }

    private void ActualizarPagina()
    {
        paginaActual = Math.Min(paginaActual, TotalPaginas);
        foreach (var nombre in new[]
        {
            nameof(FilasVisibles),
            nameof(PaginaActual),
            nameof(TotalPaginas),
            nameof(TotalFiltradas),
            nameof(TextoPagina)
        }

        )
            Notificar(nombre);
        PaginaAnterior.Actualizar();
        PaginaSiguiente.Actualizar();
    }

    private void CambioFila(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FilaNota.Texto) or nameof(FilaNota.Letra))
        {
            Confirmado = false;
            Actualizar();
        }
    }

    private void Actualizar()
    {
        foreach (var nombre in new[]
        {
            nameof(Cambios),
            nameof(Errores),
            nameof(Pendientes),
            nameof(PuedeEditar),
            nameof(PuedeGuardar),
            nameof(Guardando),
            nameof(Confirmado),
            nameof(Resumen),
            nameof(TextoPendientes),
            nameof(Instruccion),
            nameof(Mensaje),
            nameof(EstadoCarga),
            nameof(CargaPermitida),
            nameof(MostrarNumeros),
            nameof(MostrarLetras),
            nameof(RequiereRevision),
            nameof(TituloSalida),
            nameof(AvisoSalida)
        }

        )
            Notificar(nombre);
        PaginaAnterior.Actualizar();
        PaginaSiguiente.Actualizar();
    }

    private void Notificar([CallerMemberName] string? nombre = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
}
