using Clarisa.Instalacion.Contratos;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Sistema;
using Clarisa.Instalacion.Paquete;

namespace Clarisa.Instalacion.Ejecucion;

internal sealed class ContextoEjecucion(
    PlanInstalacion plan,
    IOperadorInstalacion operador,
    SistemaInstalacion sistema,
    RegistroInstalacion registro,
    FiltroSecretos filtro,
    EstadoPersistido estado,
    string? huellaManifiesto = null)
{
    private readonly Lazy<ManifiestoPaquete> paquete = new(() => ManifiestoPaquete.Leer(plan.Solicitud.OrigenPaquete, huellaManifiesto));
    public ManifiestoPaquete Paquete => paquete.Value;
    public PlanInstalacion Plan { get; } = plan;
    public IOperadorInstalacion Operador { get; } = operador;
    public SistemaInstalacion Sistema { get; } = sistema;
    public FiltroSecretos Filtro { get; } = filtro;
    public EstadoPersistido Estado { get; } = estado;
    // Hechos comprobados durante esta ejecución por la propia acción (por ejemplo, la salida verificada de la migración).
    public Dictionary<string, string> Hechos { get; } = new(StringComparer.Ordinal);
    // Se fija una vez por ejecución (ver ConfiguracionEfectiva); nunca se mezcla con otra fuente dentro de la misma ejecución.
    public Pasos.Servidor.FuenteConfiguracion? Fuente { get; set; }

    public SolicitudServidor Servidor => Plan.Solicitud as SolicitudServidor
        ?? throw new InvalidOperationException("El paso es exclusivo del servidor.");

    public SolicitudEstacion Estacion => Plan.Solicitud as SolicitudEstacion
        ?? throw new InvalidOperationException("El paso es exclusivo de la estación.");

    public RutasServidor RutasServidor => new(Servidor);

    // Solo existe si primera-cuenta creó la cuenta en esta ejecución; nunca pasa a Hechos, estado ni punto. El motor la dispone al terminar.
    private CredencialOperador? credencialPrimeraCuenta;

    public void ConservarCredencialPrimeraCuenta(CredencialOperador credencial)
    {
        credencialPrimeraCuenta?.Dispose();
        credencialPrimeraCuenta = credencial;
    }

    // Quien la toma pasa a disponerla.
    public CredencialOperador? TomarCredencialPrimeraCuenta()
    {
        var credencial = credencialPrimeraCuenta;
        credencialPrimeraCuenta = null;
        return credencial is { Disponible: true } ? credencial : null;
    }

    public void DescartarCredenciales() => TomarCredencialPrimeraCuenta()?.Dispose();

    public void Anotar(string etapa, string detalle) => registro.Escribir(etapa, "detalle", detalle);

    public void Propio(TipoArtefacto tipo, string identificador, bool conservar = false)
    {
        Estado.RegistrarArtefacto(tipo, identificador, conservar, Sistema.Reloj());
        Estado.Guardar(Plan.DirectorioRegistros);
    }
}
