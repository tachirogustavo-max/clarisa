using Clarisa.Domain.Common;
using Clarisa.Domain.Exceptions;

namespace Clarisa.Domain.Entities;

public sealed class Calificacion : Entidad
{
    public const int MaximoMotivo = 200;
    public const int MaximoInasistencias = 999;

    private int? inasistencias;

    public Calificacion(Guid id, Guid inscripcionId, Guid materiaId, Guid lapsoId, decimal? valor, long version)
        : this(id, inscripcionId, materiaId, lapsoId, valor is null ? TipoResultado.Pendiente : TipoResultado.Numerico, valor, null, null,
            version)
    {
    }

    public Calificacion(Guid id, Guid inscripcionId, Guid materiaId, Guid lapsoId, TipoResultado tipo, decimal? valor, string? letra,
        string? motivoExoneracion, long version) : base(id, version)
    {
        InscripcionId = Requerido(inscripcionId, "InscripcionId", "la inscripción");
        MateriaId = Requerido(materiaId, "MateriaId", "la materia");
        LapsoId = Requerido(lapsoId, "LapsoId", "el momento");
        Asignar(tipo, valor, letra, motivoExoneracion);
    }

    public Guid InscripcionId { get; }
    public Guid MateriaId { get; }
    public Guid LapsoId { get; }
    public TipoResultado Tipo { get; private set; }
    public decimal? Valor { get; private set; }
    public string? Letra { get; private set; }
    public string? MotivoExoneracion { get; private set; }

    // Horas o clases a las que faltó en esta materia y momento; nulo si todavía no se registraron. Una exoneración no las lleva.
    public int? Inasistencias
    {
        get => inasistencias;
        init => inasistencias = ValidarInasistencias(value, Tipo);
    }

    public bool Pendiente => Tipo == TipoResultado.Pendiente;
    public bool Exonerada => Tipo == TipoResultado.Exonerado;

    public static TipoResultado TipoDe(decimal? valor, string? letra) => (valor, letra) switch
    {
        (null, null) => TipoResultado.Pendiente,
        (not null, null) => TipoResultado.Numerico,
        (null, not null) => TipoResultado.Cualitativo,
        _ => TipoResultado.NumericoYCualitativo
    };

    public bool MismoResultado(decimal? valor, string? letra, int? faltas) =>
        !Exonerada && Valor == valor && Letra == letra && Inasistencias == faltas;

    // La fila se conserva pendiente al borrar una nota para que su versión no vuelva a empezar.
    public void CambiarValor(decimal? valor) => Registrar(valor, null);

    public void Registrar(decimal? valor, string? letra) => Registrar(valor, letra, Inasistencias);

    public void Registrar(decimal? valor, string? letra, int? faltas)
    {
        if (Exonerada) throw new ReglaDominioException("Valor", "El resultado está exonerado: la dirección debe revocar la exoneración.");
        var normalizada = letra is null ? null : LetraCualitativa.Normalizar(letra);
        var tipo = TipoDe(valor, normalizada);
        var validadas = ValidarInasistencias(faltas, tipo);
        Asignar(tipo, valor, normalizada, null);
        inasistencias = validadas;
    }

    public void Exonerar(string? motivo)
    {
        Asignar(TipoResultado.Exonerado, null, null, motivo);
        inasistencias = null;
    }

    public static int? ValidarInasistencias(int? faltas, TipoResultado tipo)
    {
        if (faltas is null) return null;
        if (tipo == TipoResultado.Exonerado)
            throw new ReglaDominioException("Inasistencias", "Una materia exonerada no lleva inasistencias.");
        return faltas is < 0 or > MaximoInasistencias
            ? throw new ReglaDominioException("Inasistencias", $"Las inasistencias deben estar entre 0 y {MaximoInasistencias}.")
            : faltas;
    }

    public void RevocarExoneracion()
    {
        if (!Exonerada) throw new ReglaDominioException("Tipo", "El resultado no está exonerado.");
        Asignar(TipoResultado.Pendiente, null, null, null);
    }

    public static string NormalizarMotivo(string? motivo) => Textos.Requerido(motivo, "Motivo", MaximoMotivo);

    private void Asignar(TipoResultado tipo, decimal? valor, string? letra, string? motivo)
    {
        var nota = EscalaNotas.ValidarOpcional(valor, EscalaNotas.DecimalesTecnicosMaximos, "Valor");
        var normalizada = letra is null ? null : LetraCualitativa.Normalizar(letra);
        string? motivoValido = null;
        if (tipo == TipoResultado.Exonerado)
        {
            if (nota is not null || normalizada is not null)
                throw new ReglaDominioException("Tipo", "Una exoneración no lleva nota ni letra.");
            motivoValido = NormalizarMotivo(motivo);
        }
        else if (!Enum.IsDefined(tipo) || motivo is not null || TipoDe(nota, normalizada) != tipo)
            throw new ReglaDominioException("Tipo", "El tipo de resultado no coincide con los datos registrados.");
        Tipo = tipo;
        Valor = nota;
        Letra = normalizada;
        MotivoExoneracion = motivoValido;
    }
}
