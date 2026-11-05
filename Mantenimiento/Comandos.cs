using Clarisa.Application.Abstractions;
using Clarisa.Application.Features.Respaldos;
using Clarisa.Infrastructure.Respaldos.Claves;
using Clarisa.Infrastructure.Respaldos.Mantenimiento;
using Clarisa.Infrastructure.Respaldos.MariaDb;
using Clarisa.Infrastructure.Respaldos.Operaciones;

namespace Clarisa.Mantenimiento;

internal sealed class Comandos
{
    public const string Uso = """
        Uso: Clarisa.Mantenimiento <comando> (--configuracion <ruta absoluta> | --entorno | --entorno-servicio) [opciones]
          estado
          respaldo-local --operacion <Id de copia>                           (servidor detenido)
          crear-clave --exportar <archivo de recuperación nuevo>
          exportar-clave --destino <archivo de recuperación nuevo>
          importar-clave --origen <archivo de recuperación>
          verificar --archivo <copia> [--clave-recuperacion <archivo>]
          preparar --archivo <copia> [--clave-recuperacion <archivo>]     (servidor detenido)
          activar --recuperacion <Id>                                      (servidor detenido)
          revertir                                                         (servidor detenido)
          recuperar-cuenta                                                 (servidor detenido, consola elevada)
        Las credenciales del operador de MariaDB se piden en la consola; nunca se pasan como argumentos.
        """;

    private readonly ConfiguracionMantenimiento configuracion;
    private readonly AlmacenClaveRespaldos claves;
    private readonly RecuperacionLocal recuperacion;

    public Comandos(ConfiguracionMantenimiento configuracion)
    {
        this.configuracion = configuracion;
        IReloj reloj = new RelojLocal();
        claves = new AlmacenClaveRespaldos(configuracion.Respaldos);
        recuperacion = new RecuperacionLocal(new RestauradorMariaDb(configuracion.Datos, configuracion.Respaldos, claves),
            new ReferenciaBaseActivaArchivo(configuracion.Respaldos.ArchivoBaseActiva, configuracion.Datos.Base, reloj),
            new ExclusionInstanciaArchivo(configuracion.Respaldos.ArchivoExclusion),
            new RegistroOperacionesArchivo(configuracion.Respaldos), reloj);
    }

    public async Task EjecutarAsync(string comando, Opciones opciones, CancellationToken ct)
    {
        switch (comando)
        {
            case "respaldo-local": await CrearCopiaLocal.EjecutarAsync(configuracion, opciones, ct); break;
            case "recuperar-cuenta": await RecuperarCuenta.EjecutarAsync(configuracion, ct); break;
            case "estado": await EstadoAsync(ct); break;
            case "crear-clave":
                Console.WriteLine($"Clave creada (Id {claves.CrearYExportar(opciones.Ruta("exportar"))}). Guarda el archivo de recuperación fuera del servidor.");
                break;
            case "exportar-clave":
                Console.WriteLine($"Archivo de recuperación escrito para la clave {claves.Exportar(opciones.Ruta("destino"))}. Guárdalo fuera del servidor.");
                break;
            case "importar-clave":
                Console.WriteLine($"Clave {claves.Importar(opciones.Ruta("origen"))} preparada en este servidor.");
                break;
            case "verificar":
                Mostrar(await recuperacion.ValidarAsync(opciones.Ruta("archivo"), Origen(opciones), ct));
                Console.WriteLine("La copia se descifró y todas sus tablas coinciden con su manifiesto. Esto no es una restauración.");
                break;
            case "preparar": await PrepararAsync(opciones, ct); break;
            case "activar": await ActivarAsync(opciones, ct); break;
            case "revertir": await RevertirAsync(ct); break;
            default: throw new ArgumentException("Comando no reconocido.");
        }
    }

    private async Task EstadoAsync(CancellationToken ct)
    {
        var activa = await recuperacion.ConsultarActivaAsync(ct);
        Console.WriteLine($"Clave de respaldos: {(claves.IdPreparada() is { } id ? "preparada, Id " + id : "NO preparada")}");
        Console.WriteLine($"Base para el próximo arranque: {activa.Base}{(activa.Anterior is { } anterior ? $" (anterior: {anterior})" : "")}");
        foreach (var operacion in (await recuperacion.RecuperacionesAsync(ct)).Take(10))
            Console.WriteLine($"  {operacion.Id}  {operacion.SolicitadaUtc:yyyy-MM-dd HH:mm} UTC  {operacion.Estado}/{operacion.Fase}  {operacion.Mensaje}");
    }

    private async Task PrepararAsync(Opciones opciones, CancellationToken ct)
    {
        var archivo = opciones.Ruta("archivo");
        var origen = Origen(opciones);
        Mostrar(await recuperacion.ValidarAsync(archivo, origen, ct));
        Console.WriteLine("La copia se recuperará en una base NUEVA. La base actual no se modifica.");
        var (operacion, recuperada) = await recuperacion.PrepararAsync(archivo, origen, EntradaOperador.PedirCredencial(), ct);
        Console.WriteLine($"Base nueva {recuperada.Base} verificada: {recuperada.Copia.TotalFilas} registros, mismas huellas que la copia.");
        Console.WriteLine($"Recuperación {operacion.Id} pendiente de activar. Para usarla: activar --recuperacion {operacion.Id}");
    }

    private async Task ActivarAsync(Opciones opciones, CancellationToken ct)
    {
        if (!Guid.TryParse(opciones.Valor("recuperacion"), out var id)) throw new ArgumentException("Indica el Id de la recuperación.");
        if (!EntradaOperador.Confirmar("El próximo arranque del servidor usará la base recuperada y cerrará todas las sesiones.", "ACTIVAR"))
            throw new OperationCanceledException("Activación cancelada por el operador.");
        var referencia = await recuperacion.ActivarAsync(id, EntradaOperador.PedirCredencial(), ct);
        Console.WriteLine($"Activada {referencia.Base} para el próximo arranque. Base anterior conservada: {referencia.Anterior}.");
    }

    private async Task RevertirAsync(CancellationToken ct)
    {
        if (!EntradaOperador.Confirmar("El próximo arranque volverá a la base anterior.", "REVERTIR"))
            throw new OperationCanceledException("Reversión cancelada por el operador.");
        var referencia = await recuperacion.RevertirAsync(EntradaOperador.PedirCredencial(), ct);
        Console.WriteLine($"El próximo arranque usará {referencia.Base}. La base {referencia.Anterior} se conserva.");
    }

    private static OrigenClave Origen(Opciones opciones) => new(opciones.RutaOpcional("clave-recuperacion"));

    private static void Mostrar(ResumenCopia resumen)
    {
        Console.WriteLine($"Copia creada {resumen.CreadaUtc:yyyy-MM-dd HH:mm:ss} UTC, esquema {resumen.VersionEsquema}, {resumen.TotalFilas} registros:");
        foreach (var tabla in resumen.Tablas) Console.WriteLine($"  {tabla.Nombre,-28} {tabla.Filas,10}");
    }

    private sealed class RelojLocal : IReloj
    {
        public DateTimeOffset Ahora => DateTimeOffset.UtcNow;
    }
}
