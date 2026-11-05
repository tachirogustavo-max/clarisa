using System.Text.Json;
using System.Text.RegularExpressions;
using Clarisa.Application.Abstractions;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Features.Respaldos;

namespace Clarisa.Infrastructure.Respaldos.Mantenimiento;

// Archivo pequeño con el nombre de la base que usará el próximo arranque. Sin archivo rige la base de la configuración. Se sustituye
// de forma atómica: un corte deja la referencia anterior o la nueva, nunca una mezcla.
public sealed partial class ReferenciaBaseActivaArchivo(string archivo, string baseConfigurada, IReloj reloj) : IReferenciaBaseActiva
{
    private const int MaximoBytes = 4096;

    public Task<ReferenciaBase> LeerAsync(CancellationToken ct) => Task.FromResult(Leer(archivo, baseConfigurada));

    public Task<ReferenciaBase> ActivarAsync(string nueva, Guid? recuperacionId, CancellationToken ct)
    {
        ExigirNombre(nueva);
        var actual = Leer(archivo, baseConfigurada);
        var referencia = new ReferenciaBase(nueva, actual.Base, recuperacionId, reloj.Ahora);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(archivo))!);
            EscrituraAtomica.Reemplazar(archivo, JsonSerializer.SerializeToUtf8Bytes(new ArchivoReferencia(1, referencia)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new ErrorAplicacionException(TipoError.AlmacenNoDisponible,
                "No se pudo guardar la referencia de base activa. El servidor seguirá usando la base anterior.", causa: error);
        }
        return Task.FromResult(referencia);
    }

    // Para el arranque del servidor: nombre de la base que debe usar, o la configurada si no hay referencia. Un archivo dañado impide
    // iniciar en vez de volver en silencio a otra base.
    public static string BaseParaArranque(string archivo, string baseConfigurada) => Leer(archivo, baseConfigurada).Base;

    private static ReferenciaBase Leer(string archivo, string baseConfigurada)
    {
        ExigirNombre(baseConfigurada);
        try
        {
            var info = new FileInfo(archivo);
            if (!info.Exists) return new(baseConfigurada, null, null, null);
            if (info.Length is 0 or > MaximoBytes) throw Danada();
            var leido = JsonSerializer.Deserialize<ArchivoReferencia>(EscrituraAtomica.Leer(archivo));
            if (leido is not { Formato: 1, Referencia: { Base: { } baseActiva } referencia } || !NombreValido(baseActiva)
                || (referencia.Anterior is { } anterior && !NombreValido(anterior)))
                throw Danada();
            return referencia;
        }
        catch (JsonException error)
        {
            throw Danada(error);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw Error.AlmacenNoDisponible(error);
        }
    }

    private static void ExigirNombre(string nombre)
    {
        if (!NombreValido(nombre)) throw Error.DatosInvalidos("Nombre de base no válido.", "Base");
    }

    private static bool NombreValido(string nombre) => NombreBase().IsMatch(nombre);

    private static ErrorAplicacionException Danada(Exception? causa = null) =>
        new(TipoError.EstadoNoPermitido, "La referencia de base activa está dañada. Revísala con la herramienta de mantenimiento.", causa: causa);

    private sealed record ArchivoReferencia(int Formato, ReferenciaBase Referencia);

    [GeneratedRegex("^[a-z0-9_]{1,64}$")]
    private static partial Regex NombreBase();
}
