using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Design;

// Datos ficticios solo para el diseñador: sin conexión, archivos ni servidor, y sin valores que parezcan la política del liceo.
public sealed class DatosPoliticaAcademica : ModeloPoliticaAcademica
{
    public DatosPoliticaAcademica()
    {
        Anios = [new OpcionRemota(ReferenciaDiseno.AnioId, "2026-2027")];
        MostrarVersiones([
            new PoliticaRemota(Guid.NewGuid(), ReferenciaDiseno.AnioId, 1, PoliticaRemota.Borrador,
                new ReglasRemotas(null, null, null, 2, null, null, null, null, null), "Borrador de diseño",
                ["Decimales de las notas de momento", "Decimales de la definitiva", "Regla de redondeo", "Tratamiento de un momento exonerado",
                    "Pesos de los tres momentos"], DateTimeOffset.Now, null, null, 2)
        ], null);
        Informar("Datos de diseño ficticios.");
    }
}

public sealed class DatosCierresResultados : ModeloCierresResultados
{
    public DatosCierresResultados()
    {
        Anios = [new OpcionRemota(ReferenciaDiseno.AnioId, "2026-2027")];
        Secciones = [new OpcionRemota(ReferenciaDiseno.SeccionId, "1er año A")];
        Mostrar([
            new CierreSeccionRemoto(Guid.NewGuid(), ReferenciaDiseno.AnioId, ReferenciaDiseno.SeccionId, 2, Guid.NewGuid(),
                CierreSeccionRemoto.Vigente, DateTimeOffset.Now, null, null, 1),
            new CierreSeccionRemoto(Guid.NewGuid(), ReferenciaDiseno.AnioId, ReferenciaDiseno.SeccionId, 1, Guid.NewGuid(), 2,
                DateTimeOffset.Now.AddDays(-3), DateTimeOffset.Now.AddDays(-2), "Corrección de diseño", 2)
        ]);
        Informar("Datos de diseño ficticios.");
    }
}
