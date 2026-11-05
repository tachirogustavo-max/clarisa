using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Firmantes;

public sealed record NombresFirmantes(string? Director, string? CoordinadorEvaluacion, string? CoordinadorEstudios);

// Nombres de la sección Informes de la configuración del servidor; se usan mientras Dirección no registre otros.
public interface IFirmantesConfigurados
{
    NombresFirmantes Predeterminados { get; }
}

// Numero 0: todavía no hay un cambio registrado y se muestran los de la configuración.
public sealed record FirmantesDatos(NombresFirmantes Nombres, int Numero, string? Motivo, DateTimeOffset? RegistradoEn)
{
    public static FirmantesDatos Desde(FirmantesBoletin f) =>
        new(new(f.Director, f.CoordinadorEvaluacion, f.CoordinadorEstudios), f.Numero, f.Motivo, f.RegistradoEn);
}

public sealed record CambiarFirmantes(Guid Id, int NumeroEsperado, string? Director, string? CoordinadorEvaluacion, string? CoordinadorEstudios,
    string? Motivo);
