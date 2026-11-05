namespace Clarisa.Application.Features.Respaldos;

// Persistencia del estado de trabajos. Crear es atómico: con un Id existente devuelve la operación ya registrada sin modificarla.
public interface IRegistroOperacionesRespaldo
{
    Task<(OperacionRespaldo Operacion, bool Creada)> CrearSiNoExisteAsync(OperacionRespaldo nueva, CancellationToken ct);
    Task<OperacionRespaldo?> ObtenerAsync(Guid id, CancellationToken ct);
    Task GuardarAsync(OperacionRespaldo operacion, CancellationToken ct);
    Task<IReadOnlyList<OperacionRespaldo>> ListarAsync(CancellationToken ct);
}

// Snapshot consistente, empaquetado, cifrado y relectura completa en un temporal propio de la operación.
public interface IGeneradorCopias
{
    Task<CopiaPreparada> PrepararAsync(Guid operacionId, DateTimeOffset creadaUtc, CancellationToken ct);
}

public interface IAlmacenCopias
{
    bool HayEspacioSuficiente();
    Task PublicarAsync(Guid operacionId, CancellationToken ct);
    void DescartarTemporal(Guid operacionId);
    void RetirarPublicada(Guid operacionId);
    void DescartarTemporalesHuerfanos();
    Task<bool> PublicadaCoincideAsync(Guid operacionId, long bytes, string huella, CancellationToken ct);
    Task EnviarAsync(Guid operacionId, long bytes, string huella, EscritorBloques escritor, CancellationToken ct);
}

public interface IClaveRespaldos
{
    Task<bool> EstaPreparadaAsync(CancellationToken ct);
}

// Consulta de la cuenta en la base: la sesión puede seguir viva mientras un trabajo espera.
public interface IVerificadorPermisoRespaldos
{
    Task<bool> EsAdministracionActivaAsync(Guid cuentaId, CancellationToken ct);
}
