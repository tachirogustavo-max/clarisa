using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Infrastructure.Persistence.Repositorios;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence;

internal sealed class UnidadTrabajoMariaDb : UnidadMariaDb, IUnidadTrabajo
{
    public UnidadTrabajoMariaDb(MySqlConnection conexion, MySqlTransaction transaccion, ModoUnidadTrabajo modo, int segundosComando)
        : base(conexion, transaccion, modo, segundosComando)
    {
        AniosLectivos = new RepositorioAniosLectivosMariaDb(Sesion);
        Lapsos = new RepositorioLapsosMariaDb(Sesion);
        Estudiantes = new RepositorioEstudiantesMariaDb(Sesion);
        Docentes = new RepositorioDocentesMariaDb(Sesion);
        Materias = new RepositorioMateriasMariaDb(Sesion);
        Grados = new RepositorioGradosMariaDb(Sesion);
        Secciones = new RepositorioSeccionesMariaDb(Sesion);
        Inscripciones = new RepositorioInscripcionesMariaDb(Sesion);
        AsignacionesDocentes = new RepositorioAsignacionesDocentesMariaDb(Sesion);
        VentanasCarga = new RepositorioVentanasCargaMariaDb(Sesion);
        Calificaciones = new RepositorioCalificacionesMariaDb(Sesion);
        PlanEstudios = new RepositorioPlanEstudiosMariaDb(Sesion);
        EscalaCualitativa = new RepositorioEscalaCualitativaMariaDb(Sesion);
        Politicas = new RepositorioPoliticasMariaDb(Sesion);
        Cierres = new RepositorioCierresMariaDb(Sesion);
        Firmantes = new RepositorioFirmantesMariaDb(Sesion);
        Auditoria = new RegistroAuditoriaMariaDb(Sesion);
        AccesosDocentes = new RevocacionAccesosDocenteMariaDb(Sesion);
    }

    public IRepositorioAniosLectivos AniosLectivos { get; }
    public IRepositorioLapsos Lapsos { get; }
    public IRepositorioEstudiantes Estudiantes { get; }
    public IRepositorioDocentes Docentes { get; }
    public IRepositorioMaterias Materias { get; }
    public IRepositorioGrados Grados { get; }
    public IRepositorioSecciones Secciones { get; }
    public IRepositorioInscripciones Inscripciones { get; }
    public IRepositorioAsignacionesDocentes AsignacionesDocentes { get; }
    public IRepositorioVentanasCarga VentanasCarga { get; }
    public IRepositorioCalificaciones Calificaciones { get; }
    public IRepositorioPlanEstudios PlanEstudios { get; }
    public IRepositorioEscalaCualitativa EscalaCualitativa { get; }
    public IRepositorioPoliticas Politicas { get; }
    public IRepositorioCierres Cierres { get; }
    public IRepositorioFirmantes Firmantes { get; }
    public IRegistroAuditoria Auditoria { get; }
    public IRevocacionAccesosDocente AccesosDocentes { get; }
}
