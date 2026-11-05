using System.Net.Http;
using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Configuration;
internal sealed class FabricaModulos
{
    private readonly HttpClient transporte;
    private readonly Uri servidor;
    private readonly bool administracion;
    private readonly ClienteLecturas lecturas;
    private readonly ClienteOpciones opciones;
    private readonly PreparacionCatalogos preparacion;
    private readonly HashSet<IListadoCatalogo> listados = [];
    private ModeloMisMaterias? materiasDocente;
    public event Action<ModeloMisMaterias, ModeloPlanilla>? PlanillaLista;
    public FabricaModulos(HttpClient transporte, Uri servidor, bool administracion)
    {
        this.transporte = transporte;
        this.servidor = servidor;
        this.administracion = administracion;
        lecturas = new ClienteLecturas(transporte, servidor);
        opciones = new ClienteOpciones(lecturas);
        preparacion = new PreparacionCatalogos(opciones);
    }

    public object Crear(ModuloVista modulo)
    {
        // Inicio y Calificaciones del docente son la misma lista: volver de la planilla conserva año, lapso y filtros.
        if (!administracion && modulo is ModuloVista.Inicio or ModuloVista.Calificaciones && materiasDocente is not null)
            return materiasDocente;
        var modelo = CrearInterno(modulo);
        Registrar(modelo);
        return modelo;
    }

    private void Registrar(object modelo)
    {
        if (modelo is ModeloArea area)
            foreach (var entrada in area.Entradas)
                Registrar(entrada.Modelo);
        if (modelo is IListadoCatalogo listado)
            listados.Add(listado);
        if (modelo is IEditorCatalogo editor)
            editor.RegistroGuardado += (_, _) =>
            {
                foreach (var lista in listados)
                    lista.InvalidarPreparacion();
            };
    }

    private object CrearInterno(ModuloVista modulo) => modulo switch
    {
        ModuloVista.Inicio when administracion => CrearAcademico(),
        ModuloVista.Inicio or ModuloVista.Calificaciones when !administracion => materiasDocente = CrearMateriasDocente(),
        ModuloVista.Calificaciones => CrearMateriasDocente(),
        ModuloVista.Estudiantes when administracion => new ModeloArea([new("Registro", new ModeloPersonas(false, Catalogo<PersonaRemota>("estudiantes"))), new("Inscripciones", Preparar(new ModeloInscripciones(Catalogo<InscripcionRemota>("inscripciones"))))]),
        ModuloVista.Docentes when administracion => new ModeloArea([new("Registro", new ModeloPersonas(true, Catalogo<PersonaRemota>("docentes"))), new("Asignación de materias", Preparar(new ModeloAsignaciones(Catalogo<AsignacionRemota>("asignaciones"))))]),
        ModuloVista.Materias when administracion => CrearEvaluacion(),
        ModuloVista.Secciones when administracion => new ModeloArea([new("Secciones", CrearSecciones()), new("Años de estudio", new ModeloGrados(Catalogo<GradoRemoto>("grados")))]),
        ModuloVista.Boletines when administracion => new ModeloArea([new("Boletines", new ModeloBoletines(lecturas, opciones, new ClienteDocumentos(transporte, servidor))),
            new("Firmas del boletín", new ModeloFirmantes(new ClienteFirmantes(lecturas, new ClienteEvaluacion(transporte, servidor))))]),
        ModuloVista.Reportes => CrearReportes(),
        ModuloVista.Usuarios when administracion => Preparar(new ModeloUsuarios(Catalogo<CuentaRemota>("usuarios"), new ClienteCuentas(transporte, servidor))),
        ModuloVista.Respaldos when administracion => CrearRespaldos(),
        _ => throw new ArgumentOutOfRangeException(nameof(modulo))};
    private ModeloArea CrearAcademico() => new([new("Años escolares", new ModeloAnios(Catalogo<AnioRemoto>("anios-lectivos"))), new("Momentos", Preparar(new ModeloLapsos(Catalogo<LapsoRemoto>("lapsos")))), new("Carga de notas", new ModeloVentanas(opciones, lecturas, new ClienteVentanas(transporte, servidor), new ClienteCalendario(transporte, servidor)))]);
    private ModeloArea CrearEvaluacion()
    {
        var escritura = new ClienteEvaluacion(transporte, servidor);
        var escala = new ModeloEscalaAnual(new ClienteEscala(lecturas, escritura));
        escala.Preparar = async ct =>
        {
            var anios = await opciones.ConsultarAsync(TipoOpciones.Anios, null, null, ct);
            if (anios.Datos is null) return anios.Error;
            escala.EstablecerOpcionesAnio(anios.Datos);
            return null;
        };
        ModeloPlanEstudios? plan = null;
        plan = new ModeloPlanEstudios(new ClientePlanes(lecturas, escritura, id => plan!.Materias.FirstOrDefault(m => m.Id == id)?.Nombre ?? "Materia no disponible"));
        plan.Preparar = async ct =>
        {
            var anios = await opciones.ConsultarAsync(TipoOpciones.Anios, null, null, ct);
            var materias = await opciones.ConsultarAsync(TipoOpciones.Materias, null, null, ct);
            if (anios.Datos is null || materias.Datos is null) return anios.Error ?? materias.Error;
            plan.EstablecerOpcionesAnio(anios.Datos);
            plan.Materias = materias.Datos;
            if (plan.AnioLectivoId is { } elegido)
            {
                var grados = await new ClienteGrados(lecturas).ConsultarAsync(elegido, ct);
                if (grados.Datos is null) return grados.Error;
                plan.Grados = grados.Datos;
            }
            else plan.Grados = [];
            return null;
        };
        return new([new("Materias", Preparar(new ModeloMaterias(Catalogo<MateriaRemota>("materias"), new ClienteMateriaEvaluada(transporte, servidor), grados: new ClienteGrados(lecturas)))),
            new("Evaluación por grado", plan), new("Escala cualitativa", escala),
            new("Política del año", new ModeloPoliticaAcademica(opciones, new ClientePoliticas(lecturas, escritura))),
            new("Cierre de resultados", new ModeloCierresResultados(opciones, new ClientePoliticas(lecturas, escritura)))]);
    }
    private ModeloSecciones CrearSecciones()
    {
        var secciones = new ModeloSecciones(Catalogo<SeccionRemota>("secciones"));
        var grados = Catalogo<GradoRemoto>("grados");
        secciones.Preparar = async ct =>
        {
            var error = await preparacion.PrepararAsync(secciones, ct);
            if (error is not null) return error;
            var activos = await grados.ConsultarAsync(new ConsultaCatalogo(1, 100, "", false), ct);
            if (activos.Datos is null) return activos.Error ?? "No se pudieron cargar los años de estudio.";
            secciones.EstablecerGrados(activos.Datos.Elementos.Select(g => g.Nombre));
            return null;
        };
        return secciones;
    }

    private ModeloMisMaterias CrearMateriasDocente()
    {
        var modelo = new ModeloMisMaterias(administracion, Catalogo<AsignacionRemota>("asignaciones"), opciones, lecturas, new ClientePlanillas(transporte, servidor),
            administracion ? new ClienteExoneraciones(new ClienteEvaluacion(transporte, servidor)) : null);
        modelo.PlanillaLista += (origen, planilla) => PlanillaLista?.Invoke(origen, planilla);
        return modelo;
    }

    private ModeloReportes CrearReportes()
    {
        ModeloReportes? modelo = null;
        var cliente = new ClienteReporteCatalogo(lecturas, () => modelo!.Filtros(), () => modelo!.Materia?.Id, () => modelo!.Docente?.Id);
        modelo = new ModeloReportes(administracion, cliente, new ClientePdfReportes(transporte, servidor));
        modelo.Preparar = ct => preparacion.PrepararAsync(modelo, ct);
        return modelo;
    }

    private ModeloRespaldos CrearRespaldos()
    {
        var operaciones = new ClienteRespaldos(transporte, servidor, lecturas);
        return new(Catalogo<RespaldoRemoto>("respaldos"), operaciones, new ClienteDescargaRespaldos(transporte, servidor), operaciones);
    }

    private ModeloListado<T> Configurar<T>(ModeloListado<T> modelo)
    {
        modelo.Preparar = ct => preparacion.PrepararAsync(modelo, ct);
        return modelo;
    }

    private TModelo Preparar<TModelo>(TModelo modelo)
        where TModelo : class
    {
        switch (modelo)
        {
            case ModeloMaterias materias:
                Configurar(materias);
                break;
            case ModeloSecciones secciones:
                Configurar(secciones);
                break;
            case ModeloLapsos lapsos:
                Configurar(lapsos);
                break;
            case ModeloInscripciones inscripciones:
                Configurar(inscripciones);
                break;
            case ModeloAsignaciones asignaciones:
                Configurar(asignaciones);
                break;
            case ModeloUsuarios usuarios:
                Configurar(usuarios);
                break;
        }

        return modelo;
    }

    private ClienteCatalogo<T> Catalogo<T>(string recurso) => new(transporte, servidor, recurso);
}
