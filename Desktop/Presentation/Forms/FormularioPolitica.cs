using System.Globalization;
using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Presentation.Forms;

public sealed record OpcionPolitica(int? Valor, string Nombre);

// Todos los campos empiezan en «Sin definir»: el formulario no propone aprobatoria, pesos ni redondeo. El servidor vuelve a validar.
public sealed class FormularioPolitica : FormularioRegistro
{
    private static readonly OpcionPolitica SinDefinir = new(null, "Sin definir");
    private OpcionPolitica decimalesCaptura;
    private OpcionPolitica decimalesResultado;
    private OpcionPolitica formula;
    private OpcionPolitica redondeo;
    private OpcionPolitica exonerado;
    private string aprobatoria;
    private string peso1;
    private string peso2;
    private string peso3;
    private string motivo;

    public FormularioPolitica(PoliticaRemota politica)
    {
        Id = politica.Id;
        Version = politica.Version;
        var reglas = politica.Reglas;
        decimalesCaptura = Elegir(Decimales, reglas.DecimalesCaptura);
        decimalesResultado = Elegir(Decimales, reglas.DecimalesResultado);
        formula = Elegir(Formulas, reglas.Formula);
        redondeo = Elegir(Redondeos, reglas.Redondeo);
        exonerado = Elegir(Exoneraciones, reglas.Exonerado);
        aprobatoria = Texto(reglas.NotaAprobatoria);
        peso1 = Texto(reglas.PesoLapso1);
        peso2 = Texto(reglas.PesoLapso2);
        peso3 = Texto(reglas.PesoLapso3);
        motivo = politica.Motivo;
        Inicializar();
    }

    public static IReadOnlyList<OpcionPolitica> Decimales { get; } = [SinDefinir, new(0, "Sin decimales"), new(1, "Un decimal"), new(2, "Dos decimales")];
    public static IReadOnlyList<OpcionPolitica> Formulas { get; } =
        [SinDefinir, new(1, "Promedio de los tres momentos"), new(2, "Ponderada por momento (pesos que suman 100)")];
    public static IReadOnlyList<OpcionPolitica> Redondeos { get; } =
        [SinDefinir, new(1, "Truncar (sin subir)"), new(2, "Mitad hacia arriba (15,5 → 16)"), new(3, "Mitad al par (14,5 → 14; 15,5 → 16)")];
    public static IReadOnlyList<OpcionPolitica> Exoneraciones { get; } =
        [SinDefinir, new(1, "La definitiva queda pendiente"), new(2, "Se excluye ese momento del cálculo")];

    public OpcionPolitica DecimalesCaptura { get => decimalesCaptura; set => Cambiar(ref decimalesCaptura, value ?? SinDefinir); }
    public OpcionPolitica DecimalesResultado { get => decimalesResultado; set => Cambiar(ref decimalesResultado, value ?? SinDefinir); }
    public OpcionPolitica Formula
    {
        get => formula;
        set
        {
            Cambiar(ref formula, value ?? SinDefinir);
            Notificar(nameof(Ponderada));
        }
    }
    public OpcionPolitica Redondeo { get => redondeo; set => Cambiar(ref redondeo, value ?? SinDefinir); }
    public OpcionPolitica Exonerado { get => exonerado; set => Cambiar(ref exonerado, value ?? SinDefinir); }
    public string NotaAprobatoria { get => aprobatoria; set => Cambiar(ref aprobatoria, value ?? ""); }
    public string PesoLapso1 { get => peso1; set => Cambiar(ref peso1, value ?? ""); }
    public string PesoLapso2 { get => peso2; set => Cambiar(ref peso2, value ?? ""); }
    public string PesoLapso3 { get => peso3; set => Cambiar(ref peso3, value ?? ""); }
    public string Motivo { get => motivo; set => Cambiar(ref motivo, value ?? ""); }
    public bool Ponderada => formula.Valor == 2;

    protected override object EstadoActual => (decimalesCaptura.Valor, decimalesResultado.Valor, formula.Valor, redondeo.Valor, exonerado.Valor,
        aprobatoria.Trim(), peso1.Trim(), peso2.Trim(), peso3.Trim(), motivo.Trim());

    protected override IReadOnlyList<string> Campos =>
        [nameof(NotaAprobatoria), nameof(PesoLapso1), nameof(PesoLapso2), nameof(PesoLapso3), nameof(Motivo)];

    protected override string ErrorCampo(string campo) => campo switch
    {
        nameof(NotaAprobatoria) => ErrorNumero(aprobatoria, EscalaRemota.Minima, EscalaRemota.Maxima, "La nota mínima aprobatoria va de 1 a 20."),
        nameof(PesoLapso1) => ErrorPeso(peso1),
        nameof(PesoLapso2) => ErrorPeso(peso2),
        nameof(PesoLapso3) => ErrorPeso(peso3) is { Length: > 0 } error ? error : ErrorSuma(),
        nameof(Motivo) => Requerido(motivo, "el motivo del cambio", 200),
        _ => ""
    };

    public override object CrearSolicitud() => new ReglasRemotas(decimalesCaptura.Valor, decimalesResultado.Valor, Numero(aprobatoria),
        formula.Valor, redondeo.Valor, exonerado.Valor, Ponderada ? Numero(peso1) : null, Ponderada ? Numero(peso2) : null,
        Ponderada ? Numero(peso3) : null);

    private string ErrorPeso(string texto) => !Ponderada ? texto.Trim().Length > 0 ? "Los pesos solo se usan con la fórmula ponderada." : ""
        : ErrorNumero(texto, 0.01m, 100m, "Cada peso va de 0,01 a 100.");

    private string ErrorSuma() => Ponderada && Numero(peso1) is { } a && Numero(peso2) is { } b && Numero(peso3) is { } c && a + b + c != 100m
        ? "Los pesos de los tres momentos deben sumar 100." : "";

    private static string ErrorNumero(string texto, decimal minimo, decimal maximo, string mensaje)
    {
        if (texto.Trim().Length == 0) return "";
        return Numero(texto) is { } valor && valor >= minimo && valor <= maximo && decimal.Round(valor, 2) == valor ? "" : mensaje;
    }

    private static decimal? Numero(string texto) =>
        decimal.TryParse(texto.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valor) ? valor : null;

    private static string Texto(decimal? valor) => valor?.ToString("0.##", CultureInfo.GetCultureInfo("es-VE")) ?? "";

    private static OpcionPolitica Elegir(IReadOnlyList<OpcionPolitica> opciones, int? valor) => opciones.FirstOrDefault(o => o.Valor == valor) ?? SinDefinir;
}
