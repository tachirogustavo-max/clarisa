using Clarisa.Application.Abstractions.Seguridad;

namespace Clarisa.Application.Abstractions.Persistencia;

public interface IUnidadTrabajo : IAsyncDisposable
{
    IRepositorioAniosLectivos AniosLectivos { get; }
    IRepositorioLapsos Lapsos { get; }
    IRepositorioEstudiantes Estudiantes { get; }
    IRepositorioDocentes Docentes { get; }
    IRepositorioMaterias Materias { get; }
    IRepositorioGrados Grados { get; }
    IRepositorioSecciones Secciones { get; }
    IRepositorioInscripciones Inscripciones { get; }
    IRepositorioAsignacionesDocentes AsignacionesDocentes { get; }
    IRepositorioVentanasCarga VentanasCarga { get; }
    IRepositorioCalificaciones Calificaciones { get; }
    IRepositorioPlanEstudios PlanEstudios { get; }
    IRepositorioEscalaCualitativa EscalaCualitativa { get; }
    IRepositorioPoliticas Politicas { get; }
    IRepositorioCierres Cierres { get; }
    IRepositorioFirmantes Firmantes { get; }
    IRegistroAuditoria Auditoria { get; }
    IRevocacionAccesosDocente AccesosDocentes { get; }

    Task ConfirmarAsync(CancellationToken ct);
}
