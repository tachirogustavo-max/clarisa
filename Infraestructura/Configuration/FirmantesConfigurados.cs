using Clarisa.Application.Features.Firmantes;

namespace Clarisa.Infrastructure.Configuration;

public sealed class FirmantesConfigurados(ConfiguracionInformes configuracion) : IFirmantesConfigurados
{
    public NombresFirmantes Predeterminados { get; } =
        new(configuracion.Director, configuracion.CoordinadorEvaluacion, configuracion.CoordinadorEstudios);
}
