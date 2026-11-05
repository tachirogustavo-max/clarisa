using System.Buffers.Binary;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace Clarisa.Desktop.Services;

public sealed record ResultadoDescargaRespaldo(bool Guardado, string? Error);

public interface IClienteDescargaRespaldos
{
    Task<ResultadoDescargaRespaldo> GuardarAsync(RespaldoRemoto respaldo, string destino, CancellationToken ct);
}

public sealed class ClienteDescargaRespaldos(HttpClient transporte, Uri servidor) : IClienteDescargaRespaldos
{
    public const long MaximoBytes = 256L * 1024 * 1024;

    public Task<ResultadoDescargaRespaldo> GuardarAsync(RespaldoRemoto respaldo, string destino, CancellationToken ct) =>
        Task.Run(() => GuardarNucleoAsync(respaldo, destino, ct), ct);

    private async Task<ResultadoDescargaRespaldo> GuardarNucleoAsync(RespaldoRemoto respaldo, string destino, CancellationToken ct)
    {
        if (respaldo.Id == Guid.Empty || respaldo.Bytes is < 65 or > MaximoBytes || !respaldo.Verificado)
            return new(false, "Selecciona una copia verificada de hasta 256 MB.");
        if (DireccionServidor.Validar(servidor.AbsoluteUri).Error is not null)
            return new(false, "Revisa la conexión al servidor.");
        string? temporal = null;
        var temporalPropio = false;
        using var espera = CancellationTokenSource.CreateLinkedTokenSource(ct);
        espera.CancelAfter(TimeSpan.FromMinutes(5));
        try
        {
            var ruta = Path.GetFullPath(destino);
            temporal = Path.Combine(Path.GetDirectoryName(ruta)!, ".respaldo-" + Guid.NewGuid().ToString("N") + ".tmp");
            using var respuesta = await transporte.GetAsync(new Uri(servidor, "api/respaldos/" + respaldo.Id + "/archivo"),
                HttpCompletionOption.ResponseHeadersRead, espera.Token);
            if (!respuesta.IsSuccessStatusCode || respuesta.Content.Headers.ContentType?.MediaType != "application/octet-stream")
                return new(false, "No se pudo descargar la copia. Comprueba tu sesión y vuelve a consultar la lista.");
            if (respuesta.Content.Headers.ContentLength != respaldo.Bytes)
                return new(false, "El tamaño de la copia cambió o no llegó completo. Actualiza la lista.");
            await using var entrada = await respuesta.Content.ReadAsStreamAsync(espera.Token);
            var cabecera = new byte[64];
            await entrada.ReadExactlyAsync(cabecera, espera.Token);
            if (!CabeceraValida(cabecera, respaldo.Bytes)) return new(false, "El archivo recibido no corresponde al formato de respaldo.");
            byte[] huella;
            using (var calculo = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                await using var salida = new FileStream(temporal, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    64 * 1024, FileOptions.Asynchronous);
                temporalPropio = true;
                await salida.WriteAsync(cabecera, espera.Token);
                calculo.AppendData(cabecera);
                var restantes = respaldo.Bytes - cabecera.Length;
                var bloque = new byte[64 * 1024];
                while (restantes > 0)
                {
                    var leidos = await entrada.ReadAsync(bloque.AsMemory(0, (int)Math.Min(restantes, bloque.Length)), espera.Token);
                    if (leidos == 0) throw new EndOfStreamException();
                    await salida.WriteAsync(bloque.AsMemory(0, leidos), espera.Token);
                    calculo.AppendData(bloque, 0, leidos);
                    restantes -= leidos;
                }
                if (await entrada.ReadAsync(bloque.AsMemory(0, 1), espera.Token) != 0) throw new InvalidDataException();
                await salida.FlushAsync(espera.Token);
                await Task.Run(() => salida.Flush(flushToDisk: true));
                huella = calculo.GetHashAndReset();
            }
            await using (var comprobacion = File.OpenRead(temporal))
            {
                var escrita = await SHA256.HashDataAsync(comprobacion, espera.Token);
                if (!CryptographicOperations.FixedTimeEquals(huella, escrita)) throw new InvalidDataException();
            }
            espera.Token.ThrowIfCancellationRequested();
            File.Move(temporal, ruta, overwrite: true);
            return new(true, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(false, "La copia tardó demasiado. El archivo anterior se conserva.");
        }
        catch (Exception error) when (error is IOException or HttpRequestException or UnauthorizedAccessException or ArgumentException)
        {
            return new(false, "No se pudo guardar la copia. Revisa la red, el espacio y los permisos de la carpeta.");
        }
        finally
        {
            if (temporalPropio && temporal is not null)
                try { if (File.Exists(temporal)) File.Delete(temporal); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                { System.Diagnostics.Trace.TraceWarning("No se pudo retirar un temporal de respaldo: {0}.", error.GetType().Name); }
        }
    }

    private static bool CabeceraValida(byte[] cabecera, long bytes) =>
        cabecera.AsSpan(0, 8).SequenceEqual("LCETRSP1"u8)
        && BinaryPrimitives.ReadUInt16LittleEndian(cabecera.AsSpan(8, 2)) == 1
        && BinaryPrimitives.ReadUInt16LittleEndian(cabecera.AsSpan(10, 2)) == 0
        && BinaryPrimitives.ReadInt64LittleEndian(cabecera.AsSpan(40, 8)) == bytes - 64;
}
