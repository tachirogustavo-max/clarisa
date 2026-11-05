namespace Clarisa.Application.Features.Respaldos;

public enum TipoOperacionRespaldo
{
    Respaldo = 1,
    Restauracion = 2
}

public enum EstadoOperacionRespaldo
{
    Pendiente = 1,
    EnCurso = 2,
    Completa = 3,
    Fallida = 4,
    // Copia automática antigua retirada para conservar solo las más recientes.
    Retirada = 5
}

// PendienteActivar solo aplica a restauraciones: base nueva verificada, referencia activa sin cambiar.
public enum FaseOperacionRespaldo
{
    Aceptada = 1,
    Generando = 2,
    Publicando = 3,
    PendienteActivar = 4,
    Terminada = 5
}

// Huella: SHA-256 del archivo publicado o del contenido recuperado.
// InstanteCopia: momento del servidor en que empezó el snapshot; es el que figura en el manifiesto y en la lista.
public sealed record OperacionRespaldo(Guid Id, TipoOperacionRespaldo Tipo, EstadoOperacionRespaldo Estado, FaseOperacionRespaldo Fase,
    string Mensaje, Guid SolicitadaPor, DateTimeOffset SolicitadaUtc, DateTimeOffset ActualizadaUtc, string? Nombre = null,
    long? Bytes = null, string? Huella = null, DateTimeOffset? InstanteCopia = null)
{
    public bool Abierta => Estado is EstadoOperacionRespaldo.Pendiente or EstadoOperacionRespaldo.EnCurso;

    public bool CopiaPublicada => Tipo == TipoOperacionRespaldo.Respaldo && Estado == EstadoOperacionRespaldo.Completa
        && Nombre is not null && Bytes is not null && Huella is not null && InstanteCopia is not null;
}

public sealed record SolicitarRespaldo(Guid OperacionId);

public sealed record OperacionRespaldoDatos(Guid Id, string Tipo, string Estado, string Mensaje);

// Verificado: la copia se descifró y se comprobaron todas sus tablas después de escribirla. No equivale a restauración probada.
public sealed record RespaldoResumen(Guid Id, string Nombre, DateTimeOffset CreadoEn, long Bytes, bool Verificado);

public sealed record SolicitudAceptada(OperacionRespaldoDatos Operacion, bool Creada);

public sealed record InstruccionRestauracion(bool Disponible, string Mensaje, IReadOnlyList<string> Pasos);

public sealed record CopiaPreparada(long Bytes, string Huella);

public delegate ValueTask EscritorBloques(ReadOnlyMemory<byte> bloque, CancellationToken ct);

public sealed record DescargaRespaldo(Guid Id, string Nombre, long Bytes, Func<EscritorBloques, CancellationToken, Task> EnviarAsync);

internal static class MensajesRespaldo
{
    public const string Aceptada = "Solicitud registrada. La copia se preparará en el servidor; consulta su estado.";
    public const string Automatica = "Copia diaria solicitada por el servidor.";
    public const string Retirada = "Copia automática antigua retirada; se conservan las más recientes.";
    public const string Generando = "Preparando la copia en el servidor.";
    public const string Publicando = "Copia verificada; publicándola en el servidor.";
    public const string Completa = "Copia creada, cifrada y verificada después de escribirla.";
    public const string Reconciliada = "Copia verificada antes de una interrupción; su publicación se confirmó después al revisar el archivo.";
    public const string Interrumpida = "La operación se interrumpió por un reinicio del servidor. Solicite una copia nueva.";
    public const string Detenida = "El servidor se detuvo durante la copia. No se publicó ningún archivo; solicite una copia nueva.";
    public const string SinPermiso = "La cuenta que solicitó la copia ya no tiene permiso de administración. No se generó la copia.";
    public const string SinClave = "El servidor no tiene preparada la clave de respaldos. Ejecuta la preparación en el servidor.";
    public const string SinEspacio = "No hay espacio suficiente en el servidor para preparar la copia.";
    public const string FalloInterno = "No se pudo generar la copia. No se publicó ningún archivo; revisa el registro del servidor.";
}
