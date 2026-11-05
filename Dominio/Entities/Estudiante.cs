using Clarisa.Domain.Common;

namespace Clarisa.Domain.Entities;

public sealed class Estudiante : Entidad
{
    public const int MaximoNombre = 80;

    public Estudiante(Guid id, string? identificacion, string? nombres, string? apellidos, DateOnly? fechaNacimiento,
        bool activo, long version) : base(id, version)
    {
        Identificacion = Identificaciones.NormalizarOpcional(identificacion, "Identificacion");
        Nombres = Textos.Requerido(nombres, "Nombres", MaximoNombre);
        Apellidos = Textos.Requerido(apellidos, "Apellidos", MaximoNombre);
        FechaNacimiento = fechaNacimiento;
        Activo = activo;
    }

    public string? Identificacion { get; private set; }
    public string Nombres { get; private set; }
    public string Apellidos { get; private set; }
    public DateOnly? FechaNacimiento { get; private set; }
    public bool Activo { get; private set; }

    public void Modificar(string? identificacion, string? nombres, string? apellidos, DateOnly? fechaNacimiento, bool activo)
    {
        var nuevaIdentificacion = Identificaciones.NormalizarOpcional(identificacion, "Identificacion");
        var nuevosNombres = Textos.Requerido(nombres, "Nombres", MaximoNombre);
        var nuevosApellidos = Textos.Requerido(apellidos, "Apellidos", MaximoNombre);
        Identificacion = nuevaIdentificacion;
        Nombres = nuevosNombres;
        Apellidos = nuevosApellidos;
        FechaNacimiento = fechaNacimiento;
        Activo = activo;
    }

    public bool MismoContenido(Estudiante otro) =>
        Identificacion == otro.Identificacion && Nombres == otro.Nombres && Apellidos == otro.Apellidos
        && FechaNacimiento == otro.FechaNacimiento && Activo == otro.Activo;
}
