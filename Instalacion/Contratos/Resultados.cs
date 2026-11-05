namespace Clarisa.Instalacion.Contratos;

public enum EstadoPaso
{
    Pendiente,
    EnCurso,
    Aprobado,
    Fallido,
    NoComprobado,
    Omitido,
    Cancelado,
    NoEjecutado
}

public enum EstadoInstalacion
{
    Completada,
    // Todo paso crítico se ejecutó sin fallos pero alguno no pudo comprobarse: no se presenta como terminada.
    Incompleta,
    Fallida,
    Cancelada,
    Rechazada
}

public sealed record ResultadoPaso(string Id, string Nombre, bool Critico, EstadoPaso Estado, string Detalle, bool YaEstaba = false);

public sealed record AvanceInstalacion(int Indice, int Total, string PasoId, string Nombre, EstadoPaso Estado, string Detalle);

public sealed record ResultadoInstalacion(
    Guid EjecucionId,
    ModoInstalacion Modo,
    EstadoInstalacion Estado,
    IReadOnlyList<ResultadoPaso> Pasos,
    string? ArchivoRegistro,
    IReadOnlyList<string> Indicaciones)
{
    public bool Terminada => Estado == EstadoInstalacion.Completada;

    // Solo en el servidor: lo que hay que escribir en cada estación.
    public DatosEstaciones? Estaciones { get; init; }
}

public sealed record DatosEstaciones(string Direccion, string Certificado, string Huella);
