using System.Text;
using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Application.Features.Cuentas;

namespace Clarisa.Infrastructure.Seguridad;

// Archivo local UTF-8, una contraseña por línea. Solo se guardan las que la política de longitud admitiría: las demás ya se rechazan
// por longitud. Se compara tras NFC y minúsculas invariantes, para que variar mayúsculas no esquive la lista. Un archivo configurado
// que no se puede leer o supera los límites detiene el arranque: no se trunca ni se continúa con una lista parcial.
public sealed class ListaClavesProhibidasArchivo : IListaClavesProhibidas
{
    public const long MaximoBytesPredeterminado = 16L * 1024 * 1024;
    public const int MaximoEntradasPredeterminado = 100_000;

    private static readonly UTF8Encoding Utf8Estricto = new(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);

    private readonly HashSet<string> claves;

    private ListaClavesProhibidasArchivo(HashSet<string> claves) => this.claves = claves;

    public static ListaClavesProhibidasArchivo Vacia { get; } = new([]);

    public int Entradas => claves.Count;

    public bool Contiene(string claveNormalizada) => claves.Count > 0 && Clave(claveNormalizada) is { } clave && claves.Contains(clave);

    public static ListaClavesProhibidasArchivo Cargar(string ruta, long maximoBytes = MaximoBytesPredeterminado,
        int maximoEntradas = MaximoEntradasPredeterminado)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruta);
        var archivo = new FileInfo(ruta);
        if (!archivo.Exists) throw new InvalidOperationException("No existe el archivo de contraseñas prohibidas configurado.");
        if (archivo.Length > maximoBytes) throw new InvalidOperationException("El archivo de contraseñas prohibidas supera el tamaño admitido.");
        var claves = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            using var lector = new StreamReader(archivo.FullName, Utf8Estricto, detectEncodingFromByteOrderMarks: false);
            while (lector.ReadLine() is { } linea)
            {
                if (Clave(linea) is not { } clave || !claves.Add(clave)) continue;
                if (claves.Count > maximoEntradas)
                    throw new InvalidOperationException("El archivo de contraseñas prohibidas supera el número de entradas admitido.");
            }
        }
        catch (DecoderFallbackException error)
        {
            throw new InvalidOperationException("El archivo de contraseñas prohibidas no es UTF-8 válido.", error);
        }
        claves.TrimExcess();
        return new ListaClavesProhibidasArchivo(claves);
    }

    private static string? Clave(string texto)
    {
        var normalizada = PoliticaClaves.NormalizarEntrada(texto);
        if (normalizada is null) return null;
        var caracteres = normalizada.EnumerateRunes().Count();
        return caracteres is < PoliticaClaves.Minimo or > PoliticaClaves.Maximo ? null : normalizada.ToLowerInvariant();
    }
}
