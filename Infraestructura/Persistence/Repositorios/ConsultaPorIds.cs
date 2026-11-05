using Clarisa.Application.Common.Errores;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

// Consultas por lista de Id: grupos de parámetros acotados y un máximo por llamada en lugar de una lista IN sin límite.
internal static class ConsultaPorIds
{
    public const int MaximoIds = 2000;
    public const int TamanioGrupo = 200;

    public static async Task<IReadOnlyList<T>> ListarAsync<T>(SesionMariaDb sesion, string plantillaSql, IReadOnlyCollection<Guid> ids,
        Func<MySqlDataReader, T> convertir, CancellationToken ct, int limiteFilas = SesionMariaDb.LimiteListas,
        params Parametro[] adicionales)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var distintos = ids.Distinct().ToList();
        if (distintos.Count > MaximoIds)
            throw Error.EstadoNoPermitido($"La consulta supera el máximo técnico de {MaximoIds} registros por solicitud.");
        var resultado = new List<T>();
        foreach (var grupo in distintos.Chunk(TamanioGrupo))
        {
            var nombres = grupo.Select((_, indice) => "@id" + indice).ToArray();
            var parametros = grupo.Select((id, indice) => Parametros.Id(nombres[indice], id)).Concat(adicionales).ToArray();
            var restantes = limiteFilas - resultado.Count;
            resultado.AddRange(await sesion.ListarAsync(string.Format(plantillaSql, string.Join(", ", nombres)), convertir,
                restantes, ct, parametros));
        }
        return resultado;
    }
}
