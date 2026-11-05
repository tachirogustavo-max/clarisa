using System.Diagnostics;
using System.Text;

namespace Clarisa.Instalacion.Sistema.Windows;

// Siempre ArgumentList y sin intérprete de órdenes. La salida se limita en memoria; el secreto, si lo hay, va por la entrada estándar.
internal sealed class ProcesosSistema : IProcesos
{
    private const int MaximoSalida = 64 * 1024;

    public async Task<ResultadoProceso> EjecutarAsync(SolicitudProceso solicitud, CancellationToken ct)
    {
        if (!Path.IsPathFullyQualified(solicitud.Ejecutable) || !File.Exists(solicitud.Ejecutable))
            throw new Ejecucion.FalloInstalacionException($"No se encuentra el programa {Path.GetFileName(solicitud.Ejecutable)}.");
        var inicio = new ProcessStartInfo(solicitud.Ejecutable)
        {
            UseShellExecute = false,
            CreateNoWindow = !solicitud.ConsolaVisible,
            RedirectStandardOutput = !solicitud.ConsolaVisible,
            RedirectStandardError = !solicitud.ConsolaVisible,
            RedirectStandardInput = solicitud.EntradaSecreta is not null,
            WorkingDirectory = solicitud.Directorio ?? Path.GetDirectoryName(solicitud.Ejecutable)!,
            StandardOutputEncoding = solicitud.ConsolaVisible ? null : Encoding.UTF8,
            StandardErrorEncoding = solicitud.ConsolaVisible ? null : Encoding.UTF8,
            StandardInputEncoding = solicitud.EntradaSecreta is null ? null : new UTF8Encoding(false)
        };
        if (solicitud.EntradaSecreta is not null && solicitud.ConsolaVisible)
            throw new Ejecucion.FalloInstalacionException("Una entrada secreta no se combina con una consola visible.");
        foreach (var argumento in solicitud.Argumentos) inicio.ArgumentList.Add(argumento);
        if (solicitud.VariablesEntorno is { } variables)
            foreach (var (nombre, valor) in variables) inicio.Environment[nombre] = valor;
        if (solicitud.ConsolaVisible) inicio.CreateNoWindow = false;

        using var proceso = new Process { StartInfo = inicio };
        var salida = new StringBuilder();
        if (!solicitud.ConsolaVisible)
        {
            proceso.OutputDataReceived += (_, e) => Agregar(salida, e.Data);
            proceso.ErrorDataReceived += (_, e) => Agregar(salida, e.Data);
        }
        if (!proceso.Start()) throw new Ejecucion.FalloInstalacionException($"No se pudo iniciar {Path.GetFileName(solicitud.Ejecutable)}.");
        if (!solicitud.ConsolaVisible)
        {
            proceso.BeginOutputReadLine();
            proceso.BeginErrorReadLine();
        }
        if (solicitud.EntradaSecreta is { } secreto)
        {
            try
            {
                await proceso.StandardInput.WriteLineAsync(secreto.AsMemory(), ct);
                proceso.StandardInput.Close();
            }
            catch (IOException) when (proceso.HasExited)
            {
                // El hijo terminó sin leer la entrada (por ejemplo, argumentos rechazados): su código de salida explica el motivo.
            }
        }
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(solicitud.Limite);
        try
        {
            await proceso.WaitForExitAsync(limite.Token);
        }
        catch (OperationCanceledException)
        {
            proceso.Kill(entireProcessTree: true);
            await proceso.WaitForExitAsync(CancellationToken.None);
            ct.ThrowIfCancellationRequested();
            return new ResultadoProceso(proceso.ExitCode, Texto(salida), VencioLimite: true);
        }
        proceso.WaitForExit();
        return new ResultadoProceso(proceso.ExitCode, Texto(salida), VencioLimite: false);
    }

    private static void Agregar(StringBuilder salida, string? linea)
    {
        if (linea is null) return;
        lock (salida)
        {
            if (salida.Length < MaximoSalida) salida.AppendLine(linea.Length > 2000 ? linea[..2000] : linea);
        }
    }

    private static string Texto(StringBuilder salida)
    {
        lock (salida) return salida.ToString();
    }
}
