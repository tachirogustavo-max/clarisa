using Clarisa.Domain.Common;

namespace Clarisa.Domain.Entities;

public sealed class Docente : Entidad
{
    public const int MaximoNombre = 80;

    public Docente(Guid id, string? identificacion, string? nombres, string? apellidos, string? correo, bool activo, long version)
        : base(id, version)
    {
        Identificacion = Identificaciones.Normalizar(identificacion, "Identificacion");
        Nombres = Textos.Requerido(nombres, "Nombres", MaximoNombre);
        Apellidos = Textos.Requerido(apellidos, "Apellidos", MaximoNombre);
        Correo = Correos.NormalizarOpcional(correo, "Correo");
        Activo = activo;
    }

    public string Identificacion { get; private set; }
    public string Nombres { get; private set; }
    public string Apellidos { get; private set; }
    public string? Correo { get; private set; }
    public bool Activo { get; private set; }

    public void Modificar(string? identificacion, string? nombres, string? apellidos, string? correo, bool activo)
    {
        var nuevaIdentificacion = Identificaciones.Normalizar(identificacion, "Identificacion");
        var nuevosNombres = Textos.Requerido(nombres, "Nombres", MaximoNombre);
        var nuevosApellidos = Textos.Requerido(apellidos, "Apellidos", MaximoNombre);
        var nuevoCorreo = Correos.NormalizarOpcional(correo, "Correo");
        Identificacion = nuevaIdentificacion;
        Nombres = nuevosNombres;
        Apellidos = nuevosApellidos;
        Correo = nuevoCorreo;
        Activo = activo;
    }

    public bool MismoContenido(Docente otro) =>
        Identificacion == otro.Identificacion && Nombres == otro.Nombres && Apellidos == otro.Apellidos
        && Correo == otro.Correo && Activo == otro.Activo;
}
