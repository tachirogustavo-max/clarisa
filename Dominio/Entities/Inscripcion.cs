using Clarisa.Domain.Common;
using Clarisa.Domain.Exceptions;

namespace Clarisa.Domain.Entities;

public enum EstadoInscripcion
{
    Activa = 1,
    Retirada = 2
}

public sealed class Inscripcion : Entidad
{
    public Inscripcion(Guid id, Guid estudianteId, Guid seccionId, Guid anioLectivoId, DateOnly fecha,
        EstadoInscripcion estado, DateOnly? fechaRetiro, long version) : base(id, version)
    {
        EstudianteId = Requerido(estudianteId, "EstudianteId", "el estudiante");
        SeccionId = Requerido(seccionId, "SeccionId", "la sección");
        AnioLectivoId = Requerido(anioLectivoId, "AnioLectivoId", "el año lectivo");
        Fecha = fecha;
        var coherente = estado switch
        {
            EstadoInscripcion.Activa => fechaRetiro is null,
            EstadoInscripcion.Retirada => fechaRetiro is { } retiro && retiro >= fecha,
            _ => false
        };
        if (!coherente) throw new ReglaDominioException("Estado", "El estado de la inscripción no es coherente con sus fechas.");
        Estado = estado;
        FechaRetiro = fechaRetiro;
    }

    public Guid EstudianteId { get; }
    public Guid SeccionId { get; }
    public Guid AnioLectivoId { get; }
    public DateOnly Fecha { get; }
    public EstadoInscripcion Estado { get; private set; }
    public DateOnly? FechaRetiro { get; private set; }

    public void Retirar(DateOnly fecha)
    {
        if (Estado != EstadoInscripcion.Activa)
            throw new ReglaDominioException("Estado", "La inscripción ya está retirada.");
        if (fecha < Fecha)
            throw new ReglaDominioException("Fecha", "La fecha de retiro no puede ser anterior a la inscripción.");
        Estado = EstadoInscripcion.Retirada;
        FechaRetiro = fecha;
    }

    public bool MismoContenido(Inscripcion otro) =>
        EstudianteId == otro.EstudianteId && SeccionId == otro.SeccionId && AnioLectivoId == otro.AnioLectivoId
        && Fecha == otro.Fecha && Estado == otro.Estado && FechaRetiro == otro.FechaRetiro;
}
