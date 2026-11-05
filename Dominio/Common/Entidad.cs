using Clarisa.Domain.Exceptions;

namespace Clarisa.Domain.Common;

public abstract class Entidad
{
    protected Entidad(Guid id, long version)
    {
        if (id == Guid.Empty) throw new ReglaDominioException("Id", "El identificador del registro no puede estar vacío.");
        if (version < 1) throw new ReglaDominioException("Version", "La versión del registro no es válida.");
        Id = id;
        Version = version;
    }

    public Guid Id { get; }
    public long Version { get; }

    protected static Guid Requerido(Guid valor, string campo, string descripcion) =>
        valor == Guid.Empty ? throw new ReglaDominioException(campo, $"Selecciona {descripcion}.") : valor;
}
