using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.VentanasCarga;

public sealed record AbrirVentanaCarga(Guid LapsoId, long VersionEsperada, DateTimeOffset? Hasta);

public sealed record CerrarVentanaCarga(Guid LapsoId, long VersionEsperada);

public sealed record VentanaCargaDatos(Guid LapsoId, EstadoVentanaCarga? Estado, bool PermiteCarga, DateTimeOffset? AbiertaDesde,
    DateTimeOffset? Hasta, DateTimeOffset? CerradaEn, DateTimeOffset ConsultadaEn, long Version)
{
    public static VentanaCargaDatos Desde(Guid lapsoId, VentanaCarga? ventana, long version, DateTimeOffset ahoraUtc) =>
        ventana is null
            ? new(lapsoId, null, false, null, null, null, ahoraUtc, 0)
            : new(lapsoId, ventana.Estado, ventana.PermiteCargaEn(ahoraUtc), ventana.AbiertaDesde, ventana.Hasta, ventana.CerradaEn,
                ahoraUtc, version);
}
