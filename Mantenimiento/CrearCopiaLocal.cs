using System.Text.Json;
using Clarisa.Application.Abstractions;
using Clarisa.Application.Features.Respaldos;
using Clarisa.Infrastructure.Respaldos.Claves;
using Clarisa.Infrastructure.Respaldos.Mantenimiento;
using Clarisa.Infrastructure.Respaldos.MariaDb;
using Clarisa.Infrastructure.Respaldos.Operaciones;

namespace Clarisa.Mantenimiento;

internal static class CrearCopiaLocal
{
    public static async Task EjecutarAsync(ConfiguracionMantenimiento configuracion, Opciones opciones, CancellationToken ct)
    {
        if (!Guid.TryParse(opciones.Valor("operacion"), out var id) || id == Guid.Empty)
            throw new ArgumentException("Indica --operacion con un identificador nuevo de copia.");
        var respaldos = configuracion.Respaldos;
        respaldos.CrearDirectorios();
        var almacen = new AlmacenCopiasArchivo(respaldos);
        var claves = new AlmacenClaveRespaldos(respaldos);
        var caso = new CopiasMantenimiento(new RegistroOperacionesArchivo(respaldos),
            new GeneradorCopiaActiva(configuracion.Datos, respaldos, claves, almacen), almacen, claves,
            new ExclusionInstanciaArchivo(respaldos.ArchivoExclusion), new RelojMantenimiento());
        var copia = await caso.CrearAsync(id, ct);
        Console.WriteLine("COPIA_VERIFICADA " + JsonSerializer.Serialize(new
        {
            Id = copia.Id, Archivo = Path.Combine(respaldos.DirectorioCopias, id.ToString("N") + AlmacenCopiasArchivo.ExtensionCopia),
            Bytes = copia.Bytes, Sha256 = copia.Huella
        }));
    }
    private sealed class RelojMantenimiento : IReloj
    {
        public DateTimeOffset Ahora => DateTimeOffset.UtcNow;
    }
}
