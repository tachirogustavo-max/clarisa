using System.Globalization;
using Clarisa.Desktop.Services;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Models;
public partial class ModeloBoletines : ModeloObservable
{
    private readonly IClienteLecturas? cliente;
    private readonly IClienteOpciones? opciones;
    private readonly IClienteDocumentos? documentos;
    private readonly CancellationTokenSource cierre = new();
    private CancellationTokenSource? consultaActiva;
    private OpcionRemota? anio;
    private OpcionRemota? seccion;
    private OpcionRemota? estudiante;
    private static readonly OpcionRemota TodosLosLapsos = new(Guid.Empty, "Todos los momentos");
    private OpcionRemota? lapso = TodosLosLapsos;
    public ModeloBoletines(IClienteLecturas? cliente = null, IClienteOpciones? opciones = null, IClienteDocumentos? documentos = null)
    {
        this.cliente = cliente;
        this.opciones = opciones;
        this.documentos = documentos;
    }

    public async Task CargarOpcionesAsync(TipoOpciones tipo)
    {
        if (opciones is null || cierre.IsCancellationRequested)
            return;
        var anioId = anio?.Id;
        var seccionId = seccion?.Id;
        try
        {
            var respuesta = await opciones.ConsultarAsync(tipo, anioId, seccionId, cierre.Token);
            if ((tipo != TipoOpciones.Anios && anio?.Id != anioId) || (tipo == TipoOpciones.Estudiantes && seccion?.Id != seccionId))
                return;
            if (respuesta.Datos is not { } datos)
            {
                Mensaje = respuesta.Error ?? "No se pudieron cargar las opciones.";
                Notificar(nameof(Mensaje));
                return;
            }

            switch (tipo)
            {
                case TipoOpciones.Anios:
                    Anios = datos;
                    Notificar(nameof(Anios));
                    break;
                case TipoOpciones.Secciones:
                    Secciones = datos;
                    Notificar(nameof(Secciones));
                    break;
                case TipoOpciones.Estudiantes:
                    Estudiantes = datos;
                    Notificar(nameof(Estudiantes));
                    break;
                case TipoOpciones.Lapsos:
                    Lapsos = new[]
                    {
                        TodosLosLapsos
                    }.Concat(datos).ToArray();
                    Notificar(nameof(Lapsos));
                    break;
            }
        }
        catch (OperationCanceledException)when (cierre.IsCancellationRequested)
        {
        }
    }

    public IReadOnlyList<OpcionRemota> Anios { get; private set; } = [];
    public IReadOnlyList<OpcionRemota> Secciones { get; private set; } = [];
    public IReadOnlyList<OpcionRemota> Estudiantes { get; private set; } = [];
    public IReadOnlyList<OpcionRemota> Lapsos { get; private set; } = [TodosLosLapsos];

    public OpcionRemota? Lapso
    {
        get => lapso;
        set
        {
            if (Establecer(ref lapso, value))
                Invalidar();
        }
    }

    public OpcionRemota? Anio
    {
        get => anio;
        set
        {
            if (Establecer(ref anio, value))
            {
                Seccion = null;
                Lapso = TodosLosLapsos;
                Lapsos = [TodosLosLapsos];
                Notificar(nameof(Lapsos));
                Secciones = [];
                Notificar(nameof(Secciones));
                Invalidar();
            }
        }
    }

    public OpcionRemota? Seccion
    {
        get => seccion;
        set
        {
            if (Establecer(ref seccion, value))
            {
                Estudiante = null;
                Estudiantes = [];
                Notificar(nameof(Estudiantes));
                Invalidar();
            }
        }
    }

    public OpcionRemota? Estudiante
    {
        get => estudiante;
        set
        {
            if (Establecer(ref estudiante, value))
                Invalidar();
        }
    }

    public bool Cargando { get; private set; }
    public string Mensaje { get; private set; } = "Selecciona el año, la sección y el estudiante para consultar su boletín.";
    public BoletinRemoto? Boletin { get; private set; }
    public IReadOnlyList<FilaBoletin> Filas { get; private set; } = [];
    public bool TieneBoletin => Boletin is not null;
    public bool Exportando { get; private set; }
    public bool PuedeExportar => TieneBoletin && documentos is not null && !Exportando;
    public string AyudaExportacion => PuedeExportar ? "Guardar el boletín informativo con los resultados registrados y los pendientes explícitos." : "Consulta el boletín antes de guardarlo.";

    // Con seccionCompleta se descargan en un solo PDF los boletines de todos los estudiantes activos de la sección consultada.
    public async Task<byte[]?> DescargarPdfAsync(bool seccionCompleta = false)
    {
        if (!PuedeExportar || cierre.IsCancellationRequested)
            return null;
        if (!seccionCompleta && documentoConsultado is { } mostrado)
            return mostrado;
        var consultado = Boletin!;
        Exportando = true;
        Notificar(nameof(Exportando));
        Notificar(nameof(PuedeExportar));
        try
        {
            var lapsoId = Lapso?.Id is { } id && id != Guid.Empty ? id : (Guid?)null;
            var respuesta = seccionCompleta ? await documentos!.BoletinesSeccionAsync(consultado.SeccionId, lapsoId, cierre.Token)
                : await documentos!.BoletinAsync(consultado.InscripcionId, lapsoId, cierre.Token);
            cierre.Token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(Boletin, consultado))
            {
                MostrarMensaje("La selección cambió. Consulta el boletín antes de descargarlo.");
                return null;
            }

            if (respuesta.Datos is not { } contenido)
            {
                MostrarMensaje(respuesta.Error ?? "No se pudo descargar el documento.");
                return null;
            }

            return contenido;
        }
        catch (OperationCanceledException)when (cierre.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            Exportando = false;
            Notificar(nameof(Exportando));
            Notificar(nameof(PuedeExportar));
        }
    }

    public void MostrarMensaje(string mensaje)
    {
        Mensaje = mensaje;
        Notificar(nameof(Mensaje));
    }

    public string NombreEstudiante => Boletin is { } b ? b.Apellidos + ", " + b.Nombres : "";
    public string Cedula => Boletin?.Cedula ?? "Sin cédula";
    public string Contexto => Boletin is { } b ? b.AnioLectivo + " · " + b.Grado + " · Sección " + b.Seccion + (Lapso is null ? "" : " · " + Lapso.Nombre) : "";
    public string AvisoResultado => Boletin switch
    {
        null => "",
        { Cierre: { } cierre } => $"Resultados cerrados por Dirección (cierre n.º {cierre.Numero} del {cierre.CerradoEn.ToLocalTime():dd/MM/yyyy}, política n.º {Boletin.Politica.NumeroPolitica}). Para corregirlos, Dirección debe anular el cierre.",
        { Politica.ResultadoDefinitivoDisponible: true } => $"Datos provisionales con la política n.º {Boletin.Politica.NumeroPolitica}: la definitiva puede cambiar hasta que Dirección cierre los resultados.",
        _ => "La definitiva queda pendiente hasta que Dirección active una política académica completa para el año."
    };

    public void EstablecerOpciones(IEnumerable<OpcionRemota> anios, IEnumerable<OpcionRemota>? secciones = null, IEnumerable<OpcionRemota>? estudiantes = null)
    {
        Anios = anios.ToArray();
        if (secciones is not null)
            Secciones = secciones.ToArray();
        if (estudiantes is not null)
            Estudiantes = estudiantes.ToArray();
        Notificar(nameof(Anios));
        Notificar(nameof(Secciones));
        Notificar(nameof(Estudiantes));
    }

    public async Task ConsultarAsync()
    {
        if (anio is null || seccion is null || estudiante is null)
        {
            Mensaje = "Selecciona el año escolar, la sección y el estudiante.";
            Notificar(nameof(Mensaje));
            return;
        }

        if (cliente is null || Cargando || cierre.IsCancellationRequested)
            return;
        using var actual = CancellationTokenSource.CreateLinkedTokenSource(cierre.Token);
        consultaActiva = actual;
        Cargando = true;
        Notificar(nameof(Cargando));
        try
        {
            var respuesta = await cliente.ConsultarAsync<BoletinRemoto>("api/boletines?anioLectivoId=" + anio.Id + "&seccionId=" + seccion.Id + "&estudianteId=" + estudiante.Id + (Lapso?.Id is { } id && id != Guid.Empty ? "&lapsoId=" + id : ""), actual.Token);
            if (actual.IsCancellationRequested)
                return;
            if (!ValidacionBoletinRemoto.EsValido(respuesta.Datos, anio.Id, seccion.Id, estudiante.Id, Lapso?.Id))
            {
                Mensaje = respuesta.Error ?? "El boletín llegó incompleto.";
                Notificar(nameof(Mensaje));
                return;
            }

            Mostrar(respuesta.Datos!);
            await CargarVistaPreviaAsync(respuesta.Datos!, Lapso?.Id is { } elegido && elegido != Guid.Empty ? elegido : null, actual.Token);
        }
        catch (OperationCanceledException)when (actual.IsCancellationRequested)
        {
        }
        finally
        {
            Cargando = false;
            consultaActiva = null;
            Notificar(nameof(Cargando));
        }
    }

    protected void Mostrar(BoletinRemoto boletin)
    {
        OlvidarVistaPrevia();
        Boletin = boletin;
        Filas = FilasBoletin.Crear(boletin);
        Mensaje = "";
        foreach (var campo in new[]
        {
            nameof(Boletin),
            nameof(Filas),
            nameof(TieneBoletin),
            nameof(NombreEstudiante),
            nameof(Cedula),
            nameof(Contexto),
            nameof(AvisoResultado),
            nameof(Mensaje),
            nameof(PuedeExportar),
            nameof(AyudaExportacion)
        }

        )
            Notificar(campo);
    }

    private void Invalidar()
    {
        consultaActiva?.Cancel();
        Boletin = null;
        Filas = [];
        OlvidarVistaPrevia();
        Notificar(nameof(TieneBoletin));
        Notificar(nameof(Filas));
        Notificar(nameof(Boletin));
        Notificar(nameof(PuedeExportar));
        Notificar(nameof(AyudaExportacion));
    }

    public void Cancelar()
    {
        cierre.Cancel();
        consultaActiva?.Cancel();
    }
}
