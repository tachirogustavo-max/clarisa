using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Design;

// Ejemplo ficticio del primer momento: notas, una materia exonerada, otra no cursada, una con letra, inasistencias y promedio.
public sealed class DatosBoletines : ModeloBoletines
{
    public DatosBoletines()
    {
        Anio = ReferenciaDiseno.Anios[0];
        Seccion = ReferenciaDiseno.Secciones[0];
        EstablecerOpciones(ReferenciaDiseno.Anios, ReferenciaDiseno.Secciones, ReferenciaDiseno.Estudiantes);
        Estudiante = ReferenciaDiseno.Estudiantes[0];
        var lapso = ReferenciaDiseno.LapsoId;
        MateriaBoletinRemota Materia(string codigo, string nombre, string texto, int? faltas = null) =>
            new(Guid.NewGuid(), codigo, nombre, [new(lapso, 1, null) { Texto = texto, Inasistencias = faltas },
                new(Guid.NewGuid(), 2, null) { Texto = "" }, new(Guid.NewGuid(), 3, null) { Texto = "" }]);
        Mostrar(new BoletinRemoto(Guid.NewGuid(), ReferenciaDiseno.EstudianteId, "12345678", "Ana María", "Mendoza Rojas", "1er año", "A",
            "2026–2027",
            [
                Materia("CAS", "Castellano", "20"), Materia("ING", "Inglés", "20", 1), Materia("MAT", "Matemática", "EX"),
                Materia("EF", "Educación Física", "19"), Materia("AP", "Arte y Patrimonio", "20"), Materia("CN", "Ciencias Naturales", "18", 1),
                Materia("FIS", "Física", "-"), Materia("GHC", "G.H.C.", "19"), Materia("OC", "Orientación y Convivencia", "19 (A)")
            ],
            new PoliticaBoletinRemota(false, []), ReferenciaDiseno.SeccionId, ReferenciaDiseno.AnioId)
        {
            Promedios = [new(lapso, 1, "19,33", 2), new(Guid.NewGuid(), 2, "", null), new(Guid.NewGuid(), 3, "", null)]
        });
    }
}
