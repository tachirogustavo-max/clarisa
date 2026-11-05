using Clarisa.Domain.Common;
using Clarisa.Domain.Exceptions;

namespace Clarisa.Domain.Entities;

// La cuenta guarda solo la huella de la clave; el cálculo y la verificación pertenecen a un servicio criptográfico externo.
public sealed class CuentaUsuario : Entidad
{
    public const int MaximoNombre = 80;
    public const int MaximoHuella = 200;

    public CuentaUsuario(Guid id, string? usuario, string? nombre, PerfilCuenta perfil, Guid? docenteId, string? huellaClave,
        bool activo, long version) : base(id, version)
    {
        Usuario = NombresUsuario.Normalizar(usuario, "Usuario");
        Nombre = Textos.Requerido(nombre, "Nombre", MaximoNombre);
        (Perfil, DocenteId) = ValidarPerfil(perfil, docenteId);
        HuellaClave = ValidarHuella(huellaClave);
        Activo = activo;
    }

    public string Usuario { get; private set; }
    public string Nombre { get; private set; }
    public PerfilCuenta Perfil { get; private set; }
    public Guid? DocenteId { get; private set; }
    public string HuellaClave { get; private set; }
    public bool Activo { get; private set; }

    // Devuelve si cambió algo que afecta a los permisos de las sesiones abiertas.
    public bool Modificar(string? usuario, string? nombre, PerfilCuenta perfil, Guid? docenteId, bool activo)
    {
        var nuevoUsuario = NombresUsuario.Normalizar(usuario, "Usuario");
        var nuevoNombre = Textos.Requerido(nombre, "Nombre", MaximoNombre);
        var (nuevoPerfil, nuevoDocente) = ValidarPerfil(perfil, docenteId);
        var cambiaAcceso = nuevoPerfil != Perfil || nuevoDocente != DocenteId || activo != Activo;
        Usuario = nuevoUsuario;
        Nombre = nuevoNombre;
        Perfil = nuevoPerfil;
        DocenteId = nuevoDocente;
        Activo = activo;
        return cambiaAcceso;
    }

    public void CambiarHuellaClave(string? huellaClave) => HuellaClave = ValidarHuella(huellaClave);

    public bool MismosDatos(CuentaUsuario otra) =>
        Usuario == otra.Usuario && Nombre == otra.Nombre && Perfil == otra.Perfil && DocenteId == otra.DocenteId && Activo == otra.Activo;

    private static (PerfilCuenta, Guid?) ValidarPerfil(PerfilCuenta perfil, Guid? docenteId)
    {
        var docente = docenteId == Guid.Empty ? null : docenteId;
        return perfil switch
        {
            PerfilCuenta.Administracion when docente is null => (perfil, null),
            PerfilCuenta.Administracion => throw new ReglaDominioException("DocenteId",
                "Una cuenta de administración no se vincula a un docente."),
            PerfilCuenta.Docente when docente is not null => (perfil, docente),
            PerfilCuenta.Docente => throw new ReglaDominioException("DocenteId", "Selecciona el docente vinculado a la cuenta."),
            _ => throw new ReglaDominioException("Perfil", "El perfil debe ser Administracion o Docente.")
        };
    }

    private static string ValidarHuella(string? huella) =>
        huella is { Length: > 0 and <= MaximoHuella } && huella.All(c => c is > ' ' and <= '~')
            ? huella
            : throw new ReglaDominioException("Clave", "La huella de la clave no es válida.");
}
