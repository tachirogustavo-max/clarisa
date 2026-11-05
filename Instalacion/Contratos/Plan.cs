namespace Clarisa.Instalacion.Contratos;

public sealed record PasoPlaneado(string Id, string Nombre, string Descripcion, bool Critico, bool Privilegiado);

public sealed record LineaResumen(string Etiqueta, string Valor);

public sealed record PlanInstalacion(
    Guid Id,
    ModoInstalacion Modo,
    SolicitudInstalacion Solicitud,
    IReadOnlyList<PasoPlaneado> Pasos,
    IReadOnlyList<LineaResumen> Resumen,
    string DirectorioRegistros);

public sealed record ResultadoPlanificacion(PlanInstalacion? Plan, IReadOnlyList<ProblemaSolicitud> Problemas)
{
    public bool Valido => Plan is not null && Problemas.Count == 0;
}
