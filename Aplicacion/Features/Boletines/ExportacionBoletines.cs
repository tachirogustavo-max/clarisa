using Clarisa.Application.Abstractions;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Application.Features.Firmantes;

namespace Clarisa.Application.Features.Boletines;

public sealed record DocumentoBoletin(BoletinDatos Boletin, DateTimeOffset ConsultadoEn, NombresFirmantes Firmantes);
// Varios documentos salen en un solo PDF, uno por página, en el orden recibido.
public interface IGeneradorPdfBoletines
{
    Task<byte[]> GenerarAsync(IReadOnlyList<DocumentoBoletin> documentos, CancellationToken ct);
}

public sealed class ExportacionBoletines(GestionBoletines boletines, GestionFirmantes firmantes, IGeneradorPdfBoletines generador,
    IReloj reloj)
{
    public async Task<byte[]> GenerarAsync(Actor actor, Guid inscripcionId, Guid? lapsoId, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        if (inscripcionId == Guid.Empty || lapsoId == Guid.Empty) throw Error.DatosInvalidos("Selecciona una inscripción y un momento válidos.");
        var boletin = await boletines.BoletinAsync(actor, inscripcionId, ct);
        if (lapsoId is { } elegido)
        {
            if (boletin.Lapsos.All(l => l.LapsoId != elegido)) throw Error.DatosInvalidos("El momento no pertenece al año del boletín.");
            boletin = GestionBoletines.SoloLapso(boletin, elegido);
        }
        return await generador.GenerarAsync([new(boletin, reloj.Ahora, await firmantes.NombresAsync(ct))], ct);
    }

    // Boletines de todos los estudiantes activos de una sección, para imprimirlos de una vez.
    public async Task<byte[]> GenerarSeccionAsync(Actor actor, Guid seccionId, Guid? lapsoId, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        if (seccionId == Guid.Empty || lapsoId == Guid.Empty) throw Error.DatosInvalidos("Selecciona una sección y un momento válidos.");
        var lista = await boletines.BoletinesSeccionAsync(actor, seccionId, ct);
        if (lista.Count == 0) throw Error.EstadoNoPermitido("La sección no tiene estudiantes activos.");
        if (lapsoId is { } elegido)
        {
            if (lista[0].Lapsos.All(l => l.LapsoId != elegido)) throw Error.DatosInvalidos("El momento no pertenece al año de la sección.");
            lista = lista.Select(b => GestionBoletines.SoloLapso(b, elegido)).ToList();
        }
        var nombres = await firmantes.NombresAsync(ct);
        var ahora = reloj.Ahora;
        return await generador.GenerarAsync(lista.Select(b => new DocumentoBoletin(b, ahora, nombres)).ToList(), ct);
    }
}
