using Clarisa.Domain.Common;

namespace Clarisa.Domain.Entities;

public sealed class AsignacionDocente : Entidad
{
    public AsignacionDocente(Guid id, Guid seccionId, Guid materiaId, Guid docenteId, long version) : base(id, version)
    {
        SeccionId = Requerido(seccionId, "SeccionId", "la sección");
        MateriaId = Requerido(materiaId, "MateriaId", "la materia");
        DocenteId = Requerido(docenteId, "DocenteId", "el docente");
    }

    public Guid SeccionId { get; }
    public Guid MateriaId { get; }
    public Guid DocenteId { get; private set; }

    public void CambiarDocente(Guid docenteId) => DocenteId = Requerido(docenteId, "DocenteId", "el docente");

    public bool MismoContenido(AsignacionDocente otro) =>
        SeccionId == otro.SeccionId && MateriaId == otro.MateriaId && DocenteId == otro.DocenteId;
}
