using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Presentation.Models;
public sealed class FilaNota : INotifyPropertyChanged, IDataErrorInfo
{
    private string texto;
    private decimal? original;
    private readonly int decimales;
    private readonly int modalidad;
    private readonly decimal minima;
    private readonly decimal maxima;
    private int tipoOriginal;
    private string? letra;
    private string? letraOriginal;
    private string textoFaltas;
    private int? faltasOriginales;
    public event PropertyChangedEventHandler? PropertyChanged;
    public FilaNota(Guid inscripcionId, string estudiante, string? cedula, decimal? valor, long version, int decimales, EvaluacionFilaNota? evaluacion = null)
    {
        if (decimales is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(decimales));
        InscripcionId = inscripcionId;
        Estudiante = estudiante;
        Cedula = cedula ?? "Sin cédula";
        original = valor;
        Version = version;
        this.decimales = decimales;
        texto = Formatear(valor);
        modalidad = evaluacion?.Modalidad ?? ModalidadRemota.Numerica;
        minima = evaluacion?.EscalaMinima ?? EscalaRemota.Minima;
        maxima = evaluacion?.EscalaMaxima ?? EscalaRemota.Maxima;
        tipoOriginal = evaluacion?.Tipo ?? (valor is null ? 0 : 1);
        letra = letraOriginal = evaluacion?.Letra;
        Letras = evaluacion?.Letras ?? [];
        MotivoExoneracion = evaluacion?.MotivoExoneracion;
        faltasOriginales = evaluacion?.Inasistencias;
        textoFaltas = FormatearFaltas(faltasOriginales);
    }

    public Guid InscripcionId { get; }
    public string Estudiante { get; }
    public string Cedula { get; }
    public long Version { get; private set; }
    public int Modalidad => modalidad;
    public bool Exonerada => tipoOriginal == TipoResultadoRemoto.Exonerado;
    public string? MotivoExoneracion { get; private set; }
    public bool EditarNumero => !Exonerada && ModalidadRemota.AdmiteNumero(modalidad);
    public bool EditarLetra => !Exonerada && ModalidadRemota.AdmiteLetra(modalidad);
    // Una materia exonerada o no cursada no lleva inasistencias.
    public bool EditarInasistencias => !Exonerada && modalidad != ModalidadRemota.NoCursada;
    public IReadOnlyList<string> Letras { get; }
    public string TipoEvaluacion => modalidad switch
    {
        ModalidadRemota.Cualitativa => "Cualitativa",
        ModalidadRemota.NumericaYCualitativa => "Nota y letra",
        ModalidadRemota.NoCursada => "No cursada",
        _ => "Cuantitativa"
    };
    public string? Letra
    {
        get => letra;
        set
        {
            var nueva = string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
            if (letra == nueva || Exonerada) return;
            letra = nueva;
            NotificarFila();
        }
    }

    public string Texto
    {
        get => texto;
        set
        {
            if (texto == value || Exonerada)
                return;
            texto = value ?? "";
            NotificarFila();
        }
    }

    public string TextoInasistencias
    {
        get => textoFaltas;
        set
        {
            if (textoFaltas == value || !EditarInasistencias) return;
            textoFaltas = value ?? "";
            NotificarFila();
        }
    }

    public int? Inasistencias => InterpretarFaltas().Valor;
    public decimal? Valor => Interpretar().Valor;
    public string Error => ErrorResultado();

    public string this[string propiedad] => propiedad is nameof(Texto) or nameof(Letra) or nameof(TextoInasistencias) ? Error : "";
    public bool TieneError => Error.Length > 0;
    public bool Cambiada => !Exonerada && (TieneError || Valor != original || letra != letraOriginal || Inasistencias != faltasOriginales);
    public bool Pendiente => !Exonerada && modalidad != ModalidadRemota.NoCursada && Valor is null && letra is null;
    public string Estado => Exonerada ? "EX · Exonerado" : modalidad == ModalidadRemota.NoCursada ? "— · No cursada" : TieneError ? "Revisar resultado" : Cambiada ? "Sin guardar" : Pendiente ? "Pendiente" : "Registrada";

    public void Restaurar()
    {
        texto = Formatear(original);
        letra = letraOriginal;
        textoFaltas = FormatearFaltas(faltasOriginales);
        NotificarFila();
    }

    public void Confirmar(decimal? valorGuardado, long version, string? letraGuardada = null, int? tipo = null, string? motivoExoneracion = null,
        int? inasistencias = null)
    {
        faltasOriginales = inasistencias;
        textoFaltas = FormatearFaltas(inasistencias);
        original = valorGuardado;
        Version = version;
        letraOriginal = letraGuardada;
        tipoOriginal = tipo ?? (valorGuardado is not null
            ? letraGuardada is null ? TipoResultadoRemoto.Numerico : TipoResultadoRemoto.NumericoYCualitativo
            : letraGuardada is null ? TipoResultadoRemoto.Pendiente : TipoResultadoRemoto.Cualitativo);
        MotivoExoneracion = tipoOriginal == TipoResultadoRemoto.Exonerado ? motivoExoneracion : null;
        NotificarFila();
    }

    private (decimal? Valor, string Error) Interpretar()
    {
        if (Exonerada || !ModalidadRemota.AdmiteNumero(modalidad)) return (null, "");
        var limpio = texto.Trim();
        if (limpio.Length == 0)
            return (null, "");
        if (limpio.Length > 8 || limpio.Any(c => !char.IsAsciiDigit(c) && c is not '.' and not ','))
            return (null, $"Escribe una nota del {minima:0} al {maxima:0}, o deja pendiente el campo.");
        var partes = limpio.Replace(',', '.').Split('.');
        if (partes.Length > 2 || partes[0].Length == 0 || (partes.Length == 2 && partes[1].Length == 0))
            return (null, "La nota no tiene un formato válido.");
        if (partes.Length == 2 && partes[1].Length > decimales)
            return (null, decimales == 0 ? "Esta planilla admite notas enteras." : "Admite hasta " + decimales + " decimales.");
        if (!decimal.TryParse(limpio.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var numero) || numero < minima || numero > maxima)
            return (null, $"La nota debe estar entre {minima:0} y {maxima:0}.");
        return (numero, "");
    }

    private (int? Valor, string Error) InterpretarFaltas()
    {
        var limpio = textoFaltas.Trim();
        if (!EditarInasistencias || limpio.Length == 0) return (null, "");
        return limpio.Length <= 3 && limpio.All(char.IsAsciiDigit)
            ? (int.Parse(limpio, CultureInfo.InvariantCulture), "")
            : (null, "Las inasistencias son un número entero de 0 a 999.");
    }

    private static string FormatearFaltas(int? faltas) => faltas?.ToString(CultureInfo.InvariantCulture) ?? "";

    private string ErrorResultado()
    {
        if (Exonerada) return "";
        if (InterpretarFaltas().Error is { Length: > 0 } faltas) return faltas;
        var numero = Interpretar();
        if (numero.Error.Length > 0) return numero.Error;
        if (!ModalidadRemota.AdmiteNumero(modalidad) && !string.IsNullOrWhiteSpace(texto)) return "Esta evaluación no admite una nota numérica.";
        if (letra is not null && (!EditarLetra || !Letras.Contains(letra))) return "Selecciona una letra de la escala del año.";
        if (modalidad == ModalidadRemota.NumericaYCualitativa && (numero.Valor is null) != (letra is null)) return "Completa la nota y la letra, o deja ambos campos pendientes.";
        return "";
    }

    private static string Formatear(decimal? valor) => valor?.ToString("0.##", CultureInfo.GetCultureInfo("es-VE")) ?? "";
    private void NotificarFila()
    {
        foreach (var propiedad in new[]
        {
            nameof(Texto),
            nameof(Letra),
            nameof(TextoInasistencias),
            nameof(Inasistencias),
            nameof(EditarInasistencias),
            nameof(Pendiente),
            nameof(Exonerada),
            nameof(MotivoExoneracion),
            nameof(EditarNumero),
            nameof(EditarLetra),
            nameof(Valor),
            nameof(Error),
            nameof(TieneError),
            nameof(Cambiada),
            nameof(Estado),
            nameof(Version)
        }

        )
            Notificar(propiedad);
    }

    private void Notificar([CallerMemberName] string? propiedad = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
}
