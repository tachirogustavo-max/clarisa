using Clarisa.Infrastructure.Seguridad;

namespace Clarisa.Api.Security;

public sealed record OpcionesSeguridad
{
    public int HorasSesion { get; init; } = 9;
    public bool PermitirHttpLoopback { get; init; }
    public int IteracionesClave { get; init; } = ProtectorClavesPbkdf2.IteracionesPredeterminadas;

    // Sin valor: la mitad de los núcleos, entre 1 y 8.
    public int? VerificacionesSimultaneas { get; init; }

    // Sin ruta personalizada se carga la lista distribuida con el servidor.
    public string? ArchivoClavesProhibidas { get; init; }

    public static OpcionesSeguridad Leer(IConfiguration configuracion)
    {
        var opciones = configuracion.GetSection("Seguridad").Get<OpcionesSeguridad>() ?? new OpcionesSeguridad();
        if (opciones.HorasSesion is < 1 or > 24)
            throw new InvalidOperationException("Seguridad:HorasSesion debe estar entre 1 y 24.");
        if (opciones.IteracionesClave is < ProtectorClavesPbkdf2.IteracionesMinimas or > ProtectorClavesPbkdf2.IteracionesMaximas)
            throw new InvalidOperationException($"Seguridad:IteracionesClave debe estar entre {ProtectorClavesPbkdf2.IteracionesMinimas} "
                + $"y {ProtectorClavesPbkdf2.IteracionesMaximas}.");
        if (opciones.VerificacionesSimultaneas is < 1 or > 64)
            throw new InvalidOperationException("Seguridad:VerificacionesSimultaneas debe estar entre 1 y 64.");
        return opciones;
    }
}
