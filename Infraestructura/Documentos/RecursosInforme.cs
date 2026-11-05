using PdfSharp.Fonts;

namespace Clarisa.Infrastructure.Documentos;

internal sealed class RecursosInforme : IFontResolver
{
    private static readonly Lock cerrojo = new();
    private static readonly RecursosInforme instancia = new();
    internal static void Preparar()
    {
        lock (cerrojo)
        {
            if (GlobalFontSettings.FontResolver is null) GlobalFontSettings.FontResolver = instancia;
            else if (!ReferenceEquals(GlobalFontSettings.FontResolver, instancia))
                throw new InvalidOperationException("Otro resolvedor de fuentes ocupa el motor de informes.");
        }
    }
    public FontResolverInfo? ResolveTypeface(string familia, bool negrita, bool cursiva) => familia switch
    {
        "InformeSans" or "Arial" or "Courier New" => new(negrita ? "InstrumentSans-600.ttf" : "InstrumentSans-400.ttf", false, cursiva),
        "InformeSerif" => new(negrita ? "SourceSerif4-600.ttf" : "SourceSerif4-400.ttf", false, cursiva),
        "BoletinSerif" => new(negrita ? "Tinos-Bold.ttf" : "Tinos-Regular.ttf", false, cursiva),
        _ => null
    };
    public byte[] GetFont(string nombre) => Leer(nombre);
    internal static byte[] Leer(string nombre)
    {
        if (nombre is not ("InstrumentSans-400.ttf" or "InstrumentSans-600.ttf" or "SourceSerif4-400.ttf" or "SourceSerif4-600.ttf"
            or "Tinos-Regular.ttf" or "Tinos-Bold.ttf" or "logo-liceo.png" or "logo-ministerio.png"))
            throw new InvalidOperationException("El recurso del informe no está permitido.");
        using var origen = typeof(RecursosInforme).Assembly.GetManifestResourceStream("Clarisa.Informes." + nombre)
            ?? throw new InvalidOperationException("Falta un recurso de identidad del informe.");
        using var memoria = new MemoryStream();
        origen.CopyTo(memoria);
        return memoria.ToArray();
    }
}
