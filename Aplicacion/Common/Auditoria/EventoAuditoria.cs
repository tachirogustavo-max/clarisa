namespace Clarisa.Application.Common.Auditoria;

public enum AccionAuditoria
{
    Creacion = 1,
    Modificacion = 2,
    Eliminacion = 3,
    Inscripcion = 4,
    Retiro = 5,
    CambioDocente = 6,
    AperturaVentanaCarga = 7,
    CambioPlazoVentanaCarga = 8,
    CierreVentanaCarga = 9,
    CambioCalificacion = 10,
    SeleccionLapsoActual = 11,
    Exoneracion = 12,
    RevocacionExoneracion = 13,
    CreacionPolitica = 14,
    CambioPolitica = 15,
    ActivacionPolitica = 16,
    CierreResultados = 17,
    AnulacionCierre = 18,
    CambioFirmantes = 19
}

public sealed record EventoAuditoria(Guid ActorUsuarioId, AccionAuditoria Accion, string Entidad, Guid EntidadId, DateTimeOffset Ocurrido);
