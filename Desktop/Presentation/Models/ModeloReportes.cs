using Clarisa.Desktop.Services;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Models;
public class ModeloReportes : ModeloListado<FilaReporte>
{
    private IReadOnlyList<OpcionRemota> seccionesTodas = [];
    private IReadOnlyList<OpcionRemota> lapsosTodos = [];
    private OpcionRemota? seccion;
    private OpcionRemota? docente;
    private OpcionRemota? materia;
    private OpcionRemota? lapso;
    private readonly IClientePdfReportes? documentos;
    private readonly CancellationTokenSource cierreDocumento = new();
    public ModeloReportes(bool administracion, IClienteCatalogo<FilaReporte>? cliente = null, IClientePdfReportes? documentos = null) : base(cliente)
    {
        this.documentos = documentos;
        EsAdministracion = administracion;
        Titulo = administracion ? "Reportes académicos" : "Mis reportes";
        Descripcion = "Consulta las notas registradas por sección, materia y momento.";
        PropertyChanged += (_, cambio) => { if (cambio.PropertyName is nameof(Cargando) or nameof(FiltroPendiente)) Notificar(nameof(PuedeExportar)); };
    }

    public bool Exportando { get; private set; }
    public string TextoExportacion => Exportando ? "Preparando PDF…" : "Guardar PDF";
    public bool PuedeExportar => documentos is not null && Anio is not null && !Exportando && !Cargando && !FiltroPendiente;
    private FiltroReporteRemoto FiltroDocumento() => new(Anio?.Id, Seccion?.Id, null, Lapso?.Id, Materia?.Id, Docente?.Id, Busqueda.Trim());
    public async Task<byte[]?> DescargarPdfAsync()
    {
        if (!PuedeExportar || cierreDocumento.IsCancellationRequested) return null;
        var filtro = FiltroDocumento();
        Exportando = true;
        Notificar(nameof(Exportando)); Notificar(nameof(PuedeExportar)); Notificar(nameof(TextoExportacion));
        try
        {
            var respuesta = await documentos!.DescargarAsync(filtro, cierreDocumento.Token);
            cierreDocumento.Token.ThrowIfCancellationRequested();
            if (filtro != FiltroDocumento()) { MostrarMensaje("Los filtros cambiaron. Consulta el reporte antes de guardar el PDF."); return null; }
            if (respuesta.Datos is null) MostrarMensaje(respuesta.Error ?? "El informe no llegó completo.");
            return respuesta.Datos;
        }
        catch (OperationCanceledException) when (cierreDocumento.IsCancellationRequested) { return null; }
        finally { Exportando = false; Notificar(nameof(Exportando)); Notificar(nameof(PuedeExportar)); Notificar(nameof(TextoExportacion)); }
    }
    public void MostrarMensaje(string mensaje) { Mensaje = mensaje; Notificar(nameof(Mensaje)); }
    public override void Cancelar() { cierreDocumento.Cancel(); base.Cancelar(); }

    public bool EsAdministracion { get; }
    public override bool RequiereAnio => true;
    public IReadOnlyList<OpcionRemota> Anios { get; private set; } = [];
    public IReadOnlyList<OpcionRemota> Secciones => seccionesTodas.Where(s => s.AnioLectivoId == Anio?.Id && Anio is not null).ToArray();
    public IReadOnlyList<OpcionRemota> Docentes { get; private set; } = [];
    public IReadOnlyList<OpcionRemota> Materias { get; private set; } = [];
    public IReadOnlyList<OpcionRemota> Lapsos => lapsosTodos.Where(l => l.AnioLectivoId == Anio?.Id && Anio is not null).ToArray();

    public OpcionRemota? Anio
    {
        get => AnioSeleccionado;
        set
        {
            if (AnioSeleccionado == value) return;
            AnioSeleccionado = value;
            Seccion = null;
            Lapso = null;
            Notificar();
            Notificar(nameof(Secciones));
            Notificar(nameof(Lapsos));
            Notificar(nameof(PuedeExportar));
        }
    }

    public OpcionRemota? Seccion
    {
        get => seccion;
        set
        {
            if (Establecer(ref seccion, value))
                MarcarFiltroPendiente();
        }
    }

    public OpcionRemota? Docente
    {
        get => docente;
        set
        {
            if (Establecer(ref docente, value))
                MarcarFiltroPendiente();
        }
    }

    public OpcionRemota? Materia
    {
        get => materia;
        set
        {
            if (Establecer(ref materia, value))
                MarcarFiltroPendiente();
        }
    }

    public OpcionRemota? Lapso
    {
        get => lapso;
        set
        {
            if (Establecer(ref lapso, value))
                MarcarFiltroPendiente();
        }
    }

    public ConsultaDocumento Filtros() => new(Anio?.Id, Seccion?.Id, null, Lapso?.Id);
    public void EstablecerOpciones(TipoOpciones tipo, IReadOnlyList<OpcionRemota> opciones)
    {
        switch (tipo)
        {
            case TipoOpciones.Anios:
                Anios = opciones;
                EstablecerOpcionesAnio(opciones);
                Notificar(nameof(Anios));
                Notificar(nameof(Anio));
                Notificar(nameof(Secciones));
                Notificar(nameof(Lapsos));
                Notificar(nameof(PuedeExportar));
                break;
            case TipoOpciones.Secciones:
                seccionesTodas = opciones;
                Notificar(nameof(Secciones));
                break;
            case TipoOpciones.Docentes:
                Docentes = opciones;
                Notificar(nameof(Docentes));
                break;
            case TipoOpciones.Materias:
                Materias = opciones;
                Notificar(nameof(Materias));
                break;
            case TipoOpciones.Lapsos:
                lapsosTodos = opciones;
                Notificar(nameof(Lapsos));
                break;
        }
    }

    public void LimpiarFiltros()
    {
        Seccion = null;
        Docente = null;
        Materia = null;
        Lapso = null;
        Busqueda = "";
        foreach (var campo in new[]
        {
            nameof(Anio),
            nameof(Seccion),
            nameof(Docente),
            nameof(Materia),
            nameof(Lapso)
        }

        )
            Notificar(campo);
    }
}
