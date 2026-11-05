using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Design;
public sealed class DatosReportes : ModeloReportes
{
    public DatosReportes() : this(true)
    {
    }

    public DatosReportes(bool administracion) : base(administracion)
    {
        EstablecerOpciones(TipoOpciones.Anios, ReferenciaDiseno.Anios);
        EstablecerOpciones(TipoOpciones.Secciones, ReferenciaDiseno.Secciones);
        EstablecerOpciones(TipoOpciones.Materias, ReferenciaDiseno.Materias);
        EstablecerOpciones(TipoOpciones.Docentes, ReferenciaDiseno.Docentes);
        EstablecerOpciones(TipoOpciones.Lapsos, ReferenciaDiseno.Lapsos);
        DatosDiseno([new FilaReporte("Mendoza Rojas, Ana María", "12345678", "Pérez, Ana María", "Matemática", "1.º A", "Primer momento", 18, "2026-2027"), new FilaReporte("Torres Salas, José Antonio", "23456789", "Pérez, Ana María", "Matemática", "1.º A", "Primer momento", null, "2026-2027")]);
    }
}
