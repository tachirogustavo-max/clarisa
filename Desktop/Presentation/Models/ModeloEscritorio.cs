using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Presentation.Models;
public sealed class ModeloEscritorio : ModeloObservable
{
    private readonly Func<ModuloVista, object> crearModulo;
    private readonly Dictionary<ModuloVista, object> modelos = [];
    public ModeloEscritorio(SesionActual sesion, Func<ModuloVista, object> crearModulo, IClienteClavePropia? claves = null)
    {
        EsAdministracion = sesion.Perfil == "Administracion";
        NombreUsuario = sesion.Nombre;
        UsuarioAcceso = sesion.Usuario ?? "";
        this.crearModulo = crearModulo;
        CambioClave = new ModeloClavePropia(claves);
        Abrir(ModuloVista.Inicio);
    }

    public bool EsAdministracion { get; }
    public ModeloClavePropia CambioClave { get; }
    public string? AvisoSalida { get; set; }
    public IReadOnlyList<PasoGuia> PasosGuia => GuiaUso.Crear(EsAdministracion, ModuloActual);
    public string NombreUsuario { get; }
    public string UsuarioAcceso { get; }
    public string RolUsuario => EsAdministracion ? "Administrador" : "Docente";
    public string DetalleUsuario => string.IsNullOrWhiteSpace(UsuarioAcceso) ? RolUsuario : UsuarioAcceso + " · " + RolUsuario;
    public ModuloVista ModuloActual { get; private set; }
    public object Contenido { get; private set; } = null!;
    public bool MostrandoPlanilla => Contenido is ModeloPlanilla;
    public bool MostrandoDetalle => !modelos.TryGetValue(ModuloActual, out var lista) || !ReferenceEquals(lista, Contenido);
    public bool CerrandoSesion { get; private set; }
    public bool PuedeCerrarSesion => !CerrandoSesion;
    public string TextoCierreSesion => CerrandoSesion ? "Saliendo…" : "Cerrar sesión";
    public string TituloModulo => NavegacionPerfil.Crear(EsAdministracion).First(x => x.Clave == ModuloActual).Nombre;

    public void Abrir(ModuloVista modulo)
    {
        if (CerrandoSesion)
            return;
        if (!NavegacionPerfil.Crear(EsAdministracion).Any(x => x.Clave == modulo))
            return;
        if (!modelos.TryGetValue(modulo, out var contenido))
        {
            contenido = crearModulo(modulo);
            modelos.Add(modulo, contenido);
        }

        ModuloActual = modulo;
        Contenido = contenido;
        Notificar(nameof(ModuloActual));
        Notificar(nameof(Contenido));
        Notificar(nameof(MostrandoPlanilla));
        Notificar(nameof(MostrandoDetalle));
        Notificar(nameof(TituloModulo));
    }

    public bool IniciarCierreSesion()
    {
        if (CerrandoSesion)
            return false;
        CerrandoSesion = true;
        Cancelar();
        Notificar(nameof(CerrandoSesion));
        Notificar(nameof(PuedeCerrarSesion));
        Notificar(nameof(TextoCierreSesion));
        return true;
    }

    public void Cancelar()
    {
        CambioClave.Cancelar();
        if (Contenido is ModeloPlanilla activa)
            activa.Cancelar();
        foreach (var modelo in modelos.Values)
        {
            if (modelo is IListadoCatalogo listado)
                listado.Cancelar();
            if (modelo is ModeloPlanilla planilla)
                planilla.Cancelar();
            if (modelo is ModeloDirector director)
                director.Cancelar();
            if (modelo is ModeloArea area)
                area.Cancelar();
            if (modelo is ModeloBoletines boletines)
                boletines.Cancelar();
        }
    }

    public void AbrirDetalle(ModuloVista modulo, object contenido)
    {
        if (CerrandoSesion)
            return;
        if (!NavegacionPerfil.Crear(EsAdministracion).Any(e => e.Clave == modulo))
            return;
        ModuloActual = modulo;
        Contenido = contenido;
        Notificar(nameof(ModuloActual));
        Notificar(nameof(Contenido));
        Notificar(nameof(MostrandoPlanilla));
        Notificar(nameof(MostrandoDetalle));
        Notificar(nameof(TituloModulo));
    }
}
