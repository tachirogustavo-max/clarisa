using Clarisa.Application.Common.Errores;
using Clarisa.Application.Features.Cuentas;

namespace Clarisa.Api.Security;

// Modo del instalador gráfico: los datos llegan por la entrada estándar privada del proceso hijo, nunca por argumentos, variables ni
// archivos. La salida es una línea de texto fijo que no repite la entrada. Quien puede leer la configuración del servidor es quien puede
// preparar la base, igual que en la consola interactiva.
public static class PreparacionAdministradorEntrada
{
    public const string Argumento = "--crear-administrador-entrada";
    public const int Creada = 0;
    public const int EntradaNoRedirigida = 2;
    public const int CuentasExistentes = 3;
    public const int Rechazada = 5;
    public const int EntradaInvalida = 6;

    public static async Task<int> EjecutarAsync(IServiceProvider servicios, bool entradaRedirigida, Stream entrada, TextWriter salida,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(salida);
        if (!entradaRedirigida)
        {
            // En una consola la clave se vería al escribirla; ese caso usa --crear-administrador.
            await salida.WriteLineAsync("Este modo solo acepta los datos del instalador por la entrada estándar.");
            return EntradaNoRedirigida;
        }
        var datos = await LectorEntradaCuentaInicial.LeerAsync(entrada, ct);
        if (datos is null)
        {
            await salida.WriteLineAsync("ENTRADA_INVALIDA");
            return EntradaInvalida;
        }
        await using var alcance = servicios.CreateAsyncScope();
        var preparacion = alcance.ServiceProvider.GetRequiredService<PreparacionCuentas>();
        try
        {
            if (await preparacion.HayCuentasAsync(ct))
            {
                await salida.WriteLineAsync("CUENTAS_EXISTENTES");
                return CuentasExistentes;
            }
            await preparacion.CrearPrimerAdministradorAsync(new PrimerAdministrador(Guid.CreateVersion7(), datos.Usuario, datos.Nombre, datos.Clave), ct);
            await salida.WriteLineAsync("CUENTA_CREADA");
            return Creada;
        }
        catch (ErrorAplicacionException error) when (error.Tipo == TipoError.EstadoNoPermitido)
        {
            // Otra preparación ganó la transacción entre la consulta y la creación.
            await salida.WriteLineAsync("CUENTAS_EXISTENTES");
            return CuentasExistentes;
        }
        catch (ErrorAplicacionException error)
        {
            // Los mensajes del núcleo son de texto fijo y no contienen la entrada.
            await salida.WriteLineAsync($"RECHAZO {error.Campo ?? "Datos"}: {error.Message}");
            return Rechazada;
        }
    }
}
