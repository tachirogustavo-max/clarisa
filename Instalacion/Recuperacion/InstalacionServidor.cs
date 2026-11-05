using Clarisa.Instalacion.Contratos;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Pasos.Comunes;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Retirada;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Recuperacion;

// Localiza el servidor instalado en este equipo a partir de su entrada en Aplicaciones instaladas y de la referencia protegida que
// dejó el instalador, como hace la retirada. Abrir prepara el registro y el contexto con los que se ejecuta la herramienta de
// mantenimiento, y toma el mismo bloqueo que el instalador para que nunca corran a la vez.
internal sealed class InstalacionServidor : IDisposable
{
    private readonly FileStream bloqueo;
    private readonly RegistroInstalacion registro;

    private InstalacionServidor(FileStream bloqueo, RegistroInstalacion registro, ContextoEjecucion contexto)
    {
        this.bloqueo = bloqueo;
        this.registro = registro;
        Contexto = contexto;
    }

    public ContextoEjecucion Contexto { get; }
    public string Ruta => registro.Ruta;

    public sealed record Ubicacion(ReferenciaRetirada Referencia, string Carpeta, string Registros, RutasServidor Rutas);

    public static Ubicacion Localizar(SistemaInstalacion sistema)
    {
        var entrada = sistema.Aplicaciones.Leer(RegistroRetirada.ClaveServidor)
            ?? throw new FalloInstalacionException("Este equipo no tiene instalado el servidor. La recuperación se hace en el equipo servidor.");
        var partes = entrada.Desinstalar.Split('"', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var archivo = partes.LastOrDefault(p => string.Equals(Path.GetFileName(p), ArgumentosRetirada.NombreReferencia, StringComparison.OrdinalIgnoreCase))
            ?? throw new FalloInstalacionException("La entrada del servidor en Aplicaciones instaladas no tiene el formato previsto.");
        RutasSeguras.ExigirSinPuntosReparacion(archivo);
        var carpeta = Path.GetDirectoryName(archivo)!;
        var registros = Path.Combine(Path.GetDirectoryName(carpeta)!, "registros");
        foreach (var protegida in new[] { carpeta, registros })
            if (sistema.Permisos.Leer(protegida)?.Coincide(PasoRegistroRetirada.Acceso) != true)
                throw new FalloInstalacionException($"La carpeta {protegida} no tiene la protección de la instalación; no se confía en ella.");
        var referencia = ReferenciaRetirada.Leer(archivo);
        if (referencia.Modo != ModoInstalacion.Servidor
            || !string.Equals(Path.GetFullPath(Path.Combine(referencia.Solicitud.Datos!, "instalador", "retirada")), Path.GetFullPath(carpeta),
                StringComparison.OrdinalIgnoreCase))
            throw new FalloInstalacionException("La referencia de la instalación no corresponde a este servidor.");
        return new(referencia, carpeta, registros, new RutasServidor((SolicitudServidor)referencia.Reconstruir(carpeta)));
    }

    public static InstalacionServidor Abrir(SistemaInstalacion sistema, string prefijo)
    {
        var ubicacion = Localizar(sistema);
        FileStream bloqueo;
        try
        {
            bloqueo = new FileStream(Path.Combine(ubicacion.Registros, "ejecucion.bloqueo"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            throw new FalloInstalacionException("El instalador está en uso en este equipo. Espera a que termine y vuelve a intentarlo.");
        }
        try
        {
            var plan = new PlanInstalacion(Guid.NewGuid(), ModoInstalacion.Servidor, ubicacion.Rutas.Solicitud, [], [], ubicacion.Registros);
            var filtro = new FiltroSecretos();
            var registro = RegistroInstalacion.Abrir(ubicacion.Registros, plan.Id, filtro, sistema.Reloj, prefijo);
            var estado = EstadoPersistido.Cargar(ubicacion.Registros, ModoInstalacion.Servidor.ToString());
            return new(bloqueo, registro, new ContextoEjecucion(plan, new SinOperador(), sistema, registro, filtro, estado));
        }
        catch
        {
            bloqueo.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        registro.Dispose();
        bloqueo.Dispose();
    }

    private sealed class SinOperador : IOperadorInstalacion
    {
        public Task<CredencialOperador?> SolicitarClaveOperadorMotorAsync(CancellationToken ct) => Task.FromResult<CredencialOperador?>(null);
        public Task<CredencialOperador?> SolicitarAccesoPrimeraCuentaAsync(string usuario, CancellationToken ct) => Task.FromResult<CredencialOperador?>(null);
        public Task<bool> ConfirmarAsync(string mensaje, CancellationToken ct) => Task.FromResult(false);
    }
}
