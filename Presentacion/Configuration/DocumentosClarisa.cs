using Clarisa.Application.Configuration;
using Clarisa.Application.Features.Reportes;
using Clarisa.Application.Features.Boletines;
using Clarisa.Application.Features.Firmantes;
using Clarisa.Infrastructure.Configuration;
using Clarisa.Infrastructure.Documentos;

namespace Clarisa.Api.Configuration;

public static class DocumentosClarisa
{
    public static IServiceCollection AgregarDocumentosClarisa(this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddSingleton((configuracion.GetSection("Informes").Get<ConfiguracionInformes>() ?? new()).Validar());
        servicios.AddSingleton<IFirmantesConfigurados, FirmantesConfigurados>();
        servicios.AddSingleton<IGeneradorPdfReportes, GeneradorPdfReportes>();
        servicios.AddSingleton<IGeneradorPdfBoletines, GeneradorPdfBoletines>();
        foreach (var casoDeUso in CasosDeUso.Documentos) servicios.AddTransient(casoDeUso);
        return servicios;
    }
}
