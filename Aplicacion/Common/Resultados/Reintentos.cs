using Clarisa.Application.Common.Errores;

namespace Clarisa.Application.Common.Resultados;

internal static class Reintentos
{
    public static RegistroVersionado Resolver(bool mismoContenido, Guid id, long version) =>
        mismoContenido
            ? new RegistroVersionado(id, version)
            : throw Error.Duplicado("Ya existe un registro con este identificador y datos distintos.", "Id");
}
