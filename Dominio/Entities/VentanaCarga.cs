using Clarisa.Domain.Common;
using Clarisa.Domain.Exceptions;

namespace Clarisa.Domain.Entities;

public enum EstadoVentanaCarga
{
    Cerrada = 1,
    Abierta = 2
}

public sealed class VentanaCarga : Entidad
{
    public VentanaCarga(Guid id, Guid lapsoId, EstadoVentanaCarga estado, DateTimeOffset abiertaDesde, DateTimeOffset? hasta,
        DateTimeOffset? cerradaEn, long version) : base(id, version)
    {
        LapsoId = Requerido(lapsoId, "LapsoId", "el momento");
        AbiertaDesde = Instantes.ExigirUtc(abiertaDesde, "AbiertaDesde");
        Hasta = ValidarHasta(Instantes.ExigirUtc(hasta, "Hasta"), AbiertaDesde);
        CerradaEn = Instantes.ExigirUtc(cerradaEn, "CerradaEn");
        var coherente = estado switch
        {
            EstadoVentanaCarga.Abierta => CerradaEn is null,
            EstadoVentanaCarga.Cerrada => CerradaEn is { } cierre && cierre >= AbiertaDesde,
            _ => false
        };
        if (!coherente) throw new ReglaDominioException("Estado", "El estado de la ventana de carga no es coherente con sus fechas.");
        Estado = estado;
    }

    public Guid LapsoId { get; }
    public EstadoVentanaCarga Estado { get; private set; }
    public DateTimeOffset AbiertaDesde { get; private set; }
    public DateTimeOffset? Hasta { get; private set; }
    public DateTimeOffset? CerradaEn { get; private set; }

    public static VentanaCarga AbrirNueva(Guid id, Guid lapsoId, DateTimeOffset ahoraUtc, DateTimeOffset? hastaUtc) =>
        new(id, lapsoId, EstadoVentanaCarga.Abierta, ahoraUtc, hastaUtc, cerradaEn: null, version: 1);

    // Un reloj del servidor que retrocede antes de la apertura deja la carga cerrada.
    public bool PermiteCargaEn(DateTimeOffset ahoraUtc)
    {
        var ahora = Instantes.ExigirUtc(ahoraUtc, "Ahora");
        return Estado == EstadoVentanaCarga.Abierta && ahora >= AbiertaDesde && (Hasta is null || ahora < Hasta);
    }

    public void Abrir(DateTimeOffset ahoraUtc, DateTimeOffset? hastaUtc)
    {
        var ahora = Instantes.ExigirUtc(ahoraUtc, "Ahora");
        var nuevoHasta = ValidarHasta(Instantes.ExigirUtc(hastaUtc, "Hasta"), ahora);
        if (!PermiteCargaEn(ahora)) AbiertaDesde = ahora;
        Hasta = nuevoHasta;
        Estado = EstadoVentanaCarga.Abierta;
        CerradaEn = null;
    }

    public void Cerrar(DateTimeOffset ahoraUtc)
    {
        var ahora = Instantes.ExigirUtc(ahoraUtc, "Ahora");
        if (Estado != EstadoVentanaCarga.Abierta)
            throw new ReglaDominioException("Estado", "La ventana de carga ya está cerrada.");
        Estado = EstadoVentanaCarga.Cerrada;
        CerradaEn = ahora < AbiertaDesde ? AbiertaDesde : ahora;
    }

    private static DateTimeOffset? ValidarHasta(DateTimeOffset? hasta, DateTimeOffset referencia) =>
        hasta is { } limite && limite <= referencia
            ? throw new ReglaDominioException("Hasta", "El cierre programado debe ser posterior al momento de apertura.")
            : hasta;
}
