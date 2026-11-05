using Clarisa.Application.Features.AniosLectivos;
using Clarisa.Application.Features.AsignacionesDocentes;
using Clarisa.Application.Features.Boletines;
using Clarisa.Application.Features.Calificaciones;
using Clarisa.Application.Features.CierresResultados;
using Clarisa.Application.Features.ConfiguracionAcademica;
using Clarisa.Application.Features.Cuentas;
using Clarisa.Application.Features.Docentes;
using Clarisa.Application.Features.Estudiantes;
using Clarisa.Application.Features.Firmantes;
using Clarisa.Application.Features.Grados;
using Clarisa.Application.Features.Inscripciones;
using Clarisa.Application.Features.Lapsos;
using Clarisa.Application.Features.LecturasAcademicas;
using Clarisa.Application.Features.Materias;
using Clarisa.Application.Features.PoliticasAcademicas;
using Clarisa.Application.Features.Reportes;
using Clarisa.Application.Features.Secciones;
using Clarisa.Application.Features.VentanasCarga;

namespace Clarisa.Application.Configuration;

// Casos de uso sin estado propio, que el host registra con una instancia por uso. Están agrupados por la composición que aporta
// sus dependencias; los que guardan estado entre solicitudes (respaldos, acceso a cuentas) se registran aparte con su propia vida.
public static class CasosDeUso
{
    public static IReadOnlyList<Type> Academicos { get; } =
    [
        typeof(LecturasAcademicas), typeof(ConsultaReportes), typeof(GestionAniosLectivos), typeof(GestionLapsos), typeof(GestionEstudiantes),
        typeof(GestionDocentes), typeof(GestionMaterias), typeof(GestionGrados), typeof(CreacionMateriaEvaluada), typeof(GestionSecciones), typeof(GestionInscripciones),
        typeof(GestionAsignacionesDocentes), typeof(GestionVentanasCarga), typeof(GestionCalificaciones), typeof(GestionPlanEstudios),
        typeof(GestionEscalaCualitativa), typeof(GestionExoneraciones), typeof(GestionBoletines), typeof(GestionPoliticas),
        typeof(GestionCierres), typeof(ConsultaCalendario)
    ];

    public static IReadOnlyList<Type> Cuentas { get; } = [typeof(GestionCuentas), typeof(CambioClavePropia), typeof(PreparacionCuentas)];

    public static IReadOnlyList<Type> Documentos { get; } = [typeof(GestionFirmantes), typeof(ExportacionReportes), typeof(ExportacionBoletines)];
}
