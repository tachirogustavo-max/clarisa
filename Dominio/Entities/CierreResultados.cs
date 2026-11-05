using Clarisa.Domain.Common;
using Clarisa.Domain.Exceptions;

namespace Clarisa.Domain.Entities;

public enum EstadoCierre
{
    Vigente = 1,
    Anulado = 2
}

public sealed record TrazaCierre(Guid CerradoPor, DateTimeOffset CerradoEn, Guid? AnuladoPor = null, DateTimeOffset? AnuladoEn = null,
    string? MotivoAnulacion = null);

// VersionOrigen es la versión de la fila de calificación copiada; 0 si el lapso no tenía fila (pendiente).
public sealed record LapsoCerrado(int Orden, TipoResultado Tipo, decimal? Valor, string? Letra, long VersionOrigen);

// Copia fija de lo que vio la dirección al cerrar: los cambios posteriores de notas, plan o política no la alteran.
public sealed record ResultadoCerrado(Guid InscripcionId, Guid MateriaId, ModalidadEvaluacion? Modalidad, IReadOnlyList<LapsoCerrado> Lapsos,
    Definitiva Definitiva);

// Cierre de los resultados de una sección en su año, con la política vigente que se aplicó. Mientras está vigente nadie modifica
// notas de la sección; para corregir, la dirección lo anula con motivo y vuelve a cerrar, lo que genera otra copia numerada.
public sealed class CierreResultados : Entidad
{
    public CierreResultados(Guid id, Guid anioLectivoId, Guid seccionId, int numero, Guid politicaId, EstadoCierre estado, TrazaCierre traza,
        long version) : base(id, version)
    {
        AnioLectivoId = Requerido(anioLectivoId, "AnioLectivoId", "el año escolar");
        SeccionId = Requerido(seccionId, "SeccionId", "la sección");
        PoliticaId = Requerido(politicaId, "PoliticaId", "la política aplicada");
        if (numero < 1) throw new ReglaDominioException("Numero", "El número del cierre no es válido.");
        if (!Enum.IsDefined(estado)) throw new ReglaDominioException("Estado", "El estado del cierre no es válido.");
        if (traza is null || traza.CerradoPor == Guid.Empty || (estado == EstadoCierre.Anulado) != (traza.AnuladoPor is not null))
            throw new ReglaDominioException("Traza", "La trazabilidad del cierre no es coherente.");
        Numero = numero;
        Estado = estado;
        Traza = traza;
    }

    public Guid AnioLectivoId { get; }
    public Guid SeccionId { get; }
    public int Numero { get; }
    public Guid PoliticaId { get; }
    public EstadoCierre Estado { get; private set; }
    public TrazaCierre Traza { get; private set; }

    public void Anular(Guid actor, DateTimeOffset ahoraUtc, string? motivo)
    {
        if (Estado != EstadoCierre.Vigente) throw new ReglaDominioException("Estado", "El cierre ya está anulado.");
        var motivoValido = Textos.Requerido(motivo, "Motivo", PoliticaAcademica.MaximoMotivo);
        Estado = EstadoCierre.Anulado;
        Traza = Traza with { AnuladoPor = actor, AnuladoEn = Instantes.ExigirUtc(ahoraUtc, "AnuladoEn"), MotivoAnulacion = motivoValido };
    }
}
