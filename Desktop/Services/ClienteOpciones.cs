using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Services;
public enum TipoOpciones
{
    Anios,
    Secciones,
    Estudiantes,
    Docentes,
    Materias,
    Lapsos
}

public interface IClienteOpciones
{
    Task<ResultadoRemoto<IReadOnlyList<OpcionRemota>>> ConsultarAsync(TipoOpciones tipo, Guid? anioId, Guid? seccionId, CancellationToken ct);
}

public sealed class ClienteOpciones(IClienteLecturas lecturas) : IClienteOpciones
{
    public async Task<ResultadoRemoto<IReadOnlyList<OpcionRemota>>> ConsultarAsync(TipoOpciones tipo, Guid? anioId, Guid? seccionId, CancellationToken ct)
    {
        var recurso = tipo switch
        {
            TipoOpciones.Anios => "anios",
            TipoOpciones.Secciones => "secciones",
            TipoOpciones.Estudiantes => "estudiantes",
            TipoOpciones.Docentes => "docentes",
            TipoOpciones.Materias => "materias",
            TipoOpciones.Lapsos => "lapsos",
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))};
        var ruta = "api/opciones/" + recurso + "?limite=2000" + (anioId is { } anio ? "&anioLectivoId=" + anio : "") + (seccionId is { } seccion ? "&seccionId=" + seccion : "");
        var respuesta = await lecturas.ConsultarAsync<IReadOnlyList<OpcionRemota>>(ruta, ct);
        if (respuesta.Datos is { } datos && (datos.Count > 2000 || datos.Any(d => d is null || d.Id == Guid.Empty || string.IsNullOrWhiteSpace(d.Nombre)) || datos.Select(d => d.Id).Distinct().Count() != datos.Count))
            return new(null, "La lista de opciones no es válida. No se aplicaron cambios.");
        return respuesta;
    }
}
