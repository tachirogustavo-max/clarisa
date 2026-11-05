namespace Clarisa.Application.Common.Seguridad;

public sealed class Actor
{
    public Actor(Guid usuarioId, IEnumerable<Rol> roles, Guid? docenteId)
    {
        UsuarioId = usuarioId;
        Roles = (roles ?? []).Where(Enum.IsDefined).ToHashSet();
        DocenteId = docenteId == Guid.Empty ? null : docenteId;
    }

    public Guid UsuarioId { get; }
    public IReadOnlySet<Rol> Roles { get; }
    public Guid? DocenteId { get; }

    public bool Tiene(Rol rol) => UsuarioId != Guid.Empty && Roles.Contains(rol);
}
