using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Consultas;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Seguridad;

namespace Clarisa.Application.Features.Lapsos;

public sealed record LapsoConCarga(Guid Id, string Nombre, int Orden, bool Actual, bool PermiteCarga, long VersionVentana);
public sealed record CalendarioCarga(Guid AnioLectivoId, long VersionAnio, IReadOnlyList<LapsoConCarga> Lapsos)
{
    public bool PoliticaActiva { get; init; }
}

public sealed class ConsultaCalendario(IFabricaUnidadTrabajo fabrica, IReloj reloj, GestionLapsos lapsos)
{
    public async Task<CalendarioCarga> CargaAsync(Actor actor, Guid anioId, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var anio = await unidad.AniosLectivos.ObtenerAsync(anioId, ct) ?? throw Error.NoEncontrado("El año escolar no existe.");
        var momentos = await unidad.Lapsos.ListarPorAnioAsync(anioId, ct);
        var ventanas = (await unidad.VentanasCarga.ListarPorLapsosAsync(momentos.Select(l => l.Id).ToArray(), ct)).ToDictionary(v => v.LapsoId);
        var politicaActiva = await unidad.Politicas.VigenteAsync(anioId, ct) is { Completa: true };
        var ahora = reloj.Ahora;
        return new(anio.Id, anio.Version, momentos.OrderBy(l => l.Orden).Select(l =>
        {
            var ventana = ventanas.GetValueOrDefault(l.Id);
            return new LapsoConCarga(l.Id, l.Nombre, l.Orden, anio.LapsoActualId == l.Id, ventana?.PermiteCargaEn(ahora) ?? false, ventana?.Version ?? 0);
        }).ToArray()) { PoliticaActiva = politicaActiva };
    }
    public async Task<Pagina<LapsoDatos>> ListarAsync(Actor actor, Guid anioId, ConsultaPagina pagina, CancellationToken ct)
    {
        var consulta = ConsultaPagina.Validar(pagina);
        var elementos = await lapsos.ListarPorAnioAsync(actor, anioId, ct);
        return new(elementos.Skip(consulta.Desplazamiento).Take(consulta.Tamanio).ToArray(), consulta.Numero, consulta.Tamanio, elementos.Count);
    }
}
