using Clarisa.Desktop.Presentation.Commands;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Models;
public interface IListadoCatalogo
{
    bool FiltroPendiente { get; }
    bool EditorAbierto { get; }

    bool Cargando { get; }

    Task CargarAsync();
    Task BuscarAsync();
    void Cancelar();
    void InvalidarPreparacion();
}

public class ModeloListado<T> : ModeloObservable, IListadoCatalogo
{
    private readonly IClienteCatalogo<T>? cliente;
    private readonly CancellationTokenSource cierre = new();
    private CancellationTokenSource? consultaActiva;
    private long revisionConsulta;
    private string busqueda = "";
    private bool incluirInactivos;
    private int pagina = 1;
    private int tamano = 25;
    private bool preparado;
    private OpcionRemota? anioSeleccionado;
    public Func<CancellationToken, Task<string?>>? Preparar { private get; set; }

    public ModeloListado(IClienteCatalogo<T>? cliente)
    {
        this.cliente = cliente;
        PaginaAnterior = new Comando(_ => _ = CambiarPaginaAsync(pagina - 1), _ => pagina > 1 && !Cargando && !FiltroPendiente);
        PaginaSiguiente = new Comando(_ => _ = CambiarPaginaAsync(pagina + 1), _ => pagina < TotalPaginas && !Cargando && !FiltroPendiente);
    }

    public string Titulo { get; protected set; } = "Registros";
    public virtual bool EditorAbierto => false;
    public string Descripcion { get; protected set; } = "";
    public string TextoNuevo { get; protected set; } = "Nuevo registro";
    public string TextoEditar { get; protected set; } = "Editar selección";
    public virtual bool RequiereAnio => false;
    public virtual bool MostrarInactivos => true;
    public IReadOnlyList<OpcionRemota> OpcionesAnio { get; private set; } = [];

    public OpcionRemota? AnioSeleccionado
    {
        get => anioSeleccionado;
        set
        {
            if (!Establecer(ref anioSeleccionado, value))
                return;
            AnioLectivoId = value?.Id;
            Elementos = [];
            Seleccionado = default;
            Total = 0;
            pagina = 1;
            Notificar(nameof(Elementos));
            MarcarFiltroPendiente();
        }
    }

    public void EstablecerOpcionesAnio(IReadOnlyList<OpcionRemota> opciones)
    {
        OpcionesAnio = opciones;
        Notificar(nameof(OpcionesAnio));
        if (opciones.Count == 1 && AnioSeleccionado is null)
        {
            anioSeleccionado = opciones[0];
            AnioLectivoId = opciones[0].Id;
            Notificar(nameof(AnioSeleccionado));
        }
    }

    public void InvalidarPreparacion() => preparado = false;
    public string Busqueda
    {
        get => busqueda;
        set
        {
            if (Establecer(ref busqueda, value ?? ""))
                MarcarFiltroPendiente();
        }
    }

    public bool IncluirInactivos
    {
        get => incluirInactivos;
        set
        {
            if (Establecer(ref incluirInactivos, value))
                MarcarFiltroPendiente();
        }
    }

    public bool FiltroPendiente { get; private set; }
    public bool Cargando { get; private set; }
    public string Mensaje { get; protected set; } = "";
    public IReadOnlyList<T> Elementos { get; private set; } = [];
    public bool SinResultados => !Cargando && Elementos.Count == 0;
    public T? Seleccionado { get; set; }
    public int Total { get; private set; }
    public Guid? AnioLectivoId { get; set; }
    public int PaginaActual => pagina;
    public int TotalPaginas => (int)Math.Max(1, (Total + (long)tamano - 1) / tamano);
    public IReadOnlyList<int> TamaniosPagina { get; } = [10, 25, 50, 100];

    public int TamanoPagina
    {
        get => tamano;
        set
        {
            if (!TamaniosPagina.Contains(value))
                return;
            if (Establecer(ref tamano, value))
            {
                pagina = 1;
                _ = CargarAsync();
            }
        }
    }

    public string TextoPagina => Total == 0 ? "Sin resultados" : "Página " + pagina + " de " + TotalPaginas + " · " + Total + " registros";
    public Comando PaginaAnterior { get; }
    public Comando PaginaSiguiente { get; }

    public Task BuscarAsync()
    {
        pagina = 1;
        return CargarAsync();
    }

    protected void MarcarFiltroPendiente()
    {
        ++revisionConsulta;
        var anterior = consultaActiva;
        consultaActiva = null;
        anterior?.Cancel();
        Cargando = false;
        FiltroPendiente = true;
        Mensaje = "Filtros modificados.";
        Actualizar();
    }

    private Task CambiarPaginaAsync(int nueva)
    {
        pagina = Math.Clamp(nueva, 1, TotalPaginas);
        return CargarAsync();
    }

    public async Task CargarAsync()
    {
        if (cliente is null || cierre.IsCancellationRequested)
            return;
        var revision = ++revisionConsulta;
        consultaActiva?.Cancel();
        using var actual = CancellationTokenSource.CreateLinkedTokenSource(cierre.Token);
        consultaActiva = actual;
        Cargando = true;
        Mensaje = "";
        Actualizar();
        try
        {
            if (!preparado && Preparar is not null)
            {
                var error = await Preparar(actual.Token);
                if (revision != revisionConsulta)
                    return;
                if (error is not null)
                {
                    Mensaje = error;
                    return;
                }

                preparado = true;
            }

            if (RequiereAnio && AnioLectivoId is null)
            {
                Mensaje = "Selecciona el año escolar para consultar los registros.";
                return;
            }

            var respuesta = await cliente.ConsultarAsync(new(pagina, tamano, busqueda.Trim(), incluirInactivos, AnioLectivoId), actual.Token);
            actual.Token.ThrowIfCancellationRequested();
            if (revision != revisionConsulta)
                return;
            if (respuesta.Datos is not { } datos || datos.Numero != pagina || datos.Tamanio != tamano || datos.Total < 0 || datos.Elementos is null || datos.Elementos.Count > tamano || datos.Elementos.Count > datos.Total || datos.Elementos.Any(e => e is null))
            {
                Mensaje = respuesta.Error ?? "La lista no llegó completa. Inténtalo nuevamente.";
                return;
            }

            Elementos = datos.Elementos;
            Total = datos.Total;
            pagina = datos.Numero;
            FiltroPendiente = false;
            Notificar(nameof(Elementos));
        }
        catch (OperationCanceledException)when (actual.IsCancellationRequested)
        {
        }
        finally
        {
            if (revision == revisionConsulta)
            {
                Cargando = false;
                Actualizar();
                consultaActiva = null;
            }
        }
    }

    protected void DatosDiseno(IEnumerable<T> filas)
    {
        Elementos = filas.ToArray();
        FiltroPendiente = false;
        Total = Elementos.Count;
        Actualizar();
        Notificar(nameof(Elementos));
    }

    public virtual void Cancelar()
    {
        cierre.Cancel();
        consultaActiva?.Cancel();
    }

    protected void Actualizar()
    {
        Notificar(nameof(SinResultados));
        foreach (var nombre in new[]
        {
            nameof(Cargando),
            nameof(Mensaje),
            nameof(PaginaActual),
            nameof(TotalPaginas),
            nameof(TextoPagina),
            nameof(Total),
            nameof(FiltroPendiente)
        }

        )
            Notificar(nombre);
        PaginaAnterior.Actualizar();
        PaginaSiguiente.Actualizar();
    }
}
