using Clarisa.Application.Common.Errores;
using Clarisa.Application.Features.Respaldos;

namespace Clarisa.Infrastructure.Respaldos.Mantenimiento;

// Archivo abierto sin compartir mientras dure el proceso. Windows lo libera si el proceso termina o se corta la energía, de modo que no
// queda un bloqueo huérfano que haya que borrar a mano. Servidor y mantenimiento deben usar la misma ruta.
public sealed class ExclusionInstanciaArchivo(string archivo) : IExclusionInstancia
{
    private const int CompartidoNoPermitido = unchecked((int)0x80070020);
    private const int BloqueoNoPermitido = unchecked((int)0x80070021);

    public IDisposable Adquirir()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(archivo))!);
            return new FileStream(archivo, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.None);
        }
        catch (IOException error) when (error.HResult is CompartidoNoPermitido or BloqueoNoPermitido)
        {
            throw new ErrorAplicacionException(TipoError.EstadoNoPermitido,
                "El servidor de la aplicación u otra tarea de mantenimiento está en ejecución. Deténgalo antes de continuar.", causa: error);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw Error.AlmacenNoDisponible(error);
        }
    }
}
