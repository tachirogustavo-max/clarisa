namespace Clarisa.Infrastructure.Configuration;

public sealed record ConfiguracionInformes
{
    public string Institucion { get; init; } = "Liceo Bolivariano Clarisa Este de Trejo";
    public string Localidad { get; init; } = "San Fernando de Apure";
    public string ZonaHoraria { get; init; } = "America/Caracas";
    public string Republica { get; init; } = "REPÚBLICA BOLIVARIANA DE VENEZUELA";
    public string Ministerio { get; init; } = "MINISTERIO DEL PODER POPULAR PARA LA EDUCACIÓN";
    public string Coordinacion { get; init; } = "COORDINACIÓN DE EVALUACIÓN Y CONTROL DE ESTUDIOS";
    public string InstitucionBoletin { get; init; } = "COMPLEJO EDUC. \"CLARISA ESTE DE TREJO\"";
    public string MinisterioBoletin { get; init; } = "MINISTERIO DEL P. P. PARA LA EDUCACIÓN";
    public string CoordinacionBoletin { get; init; } = "COORD. DE EVALUACIÓN Y CONTROL DE EST.";
    public string? PrimeraObservacionBoletin { get; init; } =
        "La aprobacion conforme a lo dispuesto en el articulo 109 del Reglamento General, no tendrá derecho a la forma de evaluación de Revisión y deberá repetir el grado, asignatura o similar.";
    public string? SegundaObservacionBoletin { get; init; } =
        "Articulo 40º.- El Boletin de Calificaciones, registrar la actuación general del alumno y servirá para mantener informado al representante, será emitido al finalizar cada Lapso y contendrá las calificaciones por área, asignatura o similar.";
    // Firmantes confirmados el 3/11/2026; se cambian en la sección Informes de la configuración del servidor.
    public string? Director { get; init; } = "Luis Argenis Parra Borjas";
    public string? CoordinadorEvaluacion { get; init; } = "Mayra Carpio";
    public string? CoordinadorEstudios { get; init; } = "Mercedes Rodríguez";
    public string? Orientador { get; init; }
    public ConfiguracionInformes Validar()
    {
        if (string.IsNullOrWhiteSpace(Institucion) || Institucion.Length > 160 || Institucion.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(Localidad) || Localidad.Length > 120 || Localidad.Any(char.IsControl))
            throw new ArgumentException("Revisa los datos de identidad de los informes.");
        _ = TimeZoneInfo.FindSystemTimeZoneById(ZonaHoraria);
        if (new[] { Republica, Ministerio, Coordinacion, InstitucionBoletin, MinisterioBoletin, CoordinacionBoletin }
            .Any(n => string.IsNullOrWhiteSpace(n) || n.Length > 160 || n.Any(char.IsControl)))
            throw new ArgumentException("Revisa el membrete de los informes.");
        if (new[] { PrimeraObservacionBoletin, SegundaObservacionBoletin }.Any(n => n is not null && (n.Length > 900 || n.Any(char.IsControl))))
            throw new ArgumentException("Revisa las observaciones del boletín.");
        if (new[] { Director, CoordinadorEvaluacion, CoordinadorEstudios, Orientador }.Any(n => n is not null && (n.Length > 120 || n.Any(char.IsControl))))
            throw new ArgumentException("Revisa los responsables de los boletines.");
        return this;
    }
}
