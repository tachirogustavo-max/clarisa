using Clarisa.Application.Common.Errores;
using Clarisa.Application.Features.LecturasAcademicas;
using Clarisa.Infrastructure.Persistence.Sesion;

namespace Clarisa.Infrastructure.Persistence.Lecturas;

internal sealed class ConsultaOpcionesMariaDb(SesionMariaDb sesion)
{
    private const string Anios = """
        SELECT a.id, a.nombre, a.id FROM anios_lectivos a
        WHERE (@anio IS NULL OR a.id = @anio)
          AND EXISTS (SELECT 1 FROM secciones s WHERE s.anio_lectivo_id = a.id
            AND (@seccion IS NULL OR s.id = @seccion)
            AND (@docente IS NULL OR EXISTS (SELECT 1 FROM asignaciones_docentes ad JOIN docentes d ON d.id = ad.docente_id
                WHERE ad.seccion_id = s.id AND ad.docente_id = @docente AND d.activo = 1)))
        ORDER BY a.inicio DESC, a.id
        """;
    private const string AniosAdministracion = """
        SELECT a.id, a.nombre, a.id FROM anios_lectivos a WHERE (@anio IS NULL OR a.id = @anio)
          AND (@seccion IS NULL OR EXISTS (SELECT 1 FROM secciones s WHERE s.anio_lectivo_id = a.id AND s.id = @seccion))
        ORDER BY a.inicio DESC, a.id
        """;
    private const string Secciones = """
        SELECT s.id, CONCAT(a.nombre, ' · ', s.grado, ' · ', s.nombre), s.anio_lectivo_id
        FROM secciones s JOIN anios_lectivos a ON a.id = s.anio_lectivo_id
        WHERE (@anio IS NULL OR a.id = @anio) AND (@seccion IS NULL OR s.id = @seccion)
          AND (@docente IS NULL OR EXISTS (SELECT 1 FROM asignaciones_docentes ad JOIN docentes d ON d.id = ad.docente_id
              WHERE ad.seccion_id = s.id AND ad.docente_id = @docente AND d.activo = 1))
        ORDER BY a.inicio DESC, s.grado, s.nombre, s.id
        """;
    private const string Lapsos = """
        SELECT l.id, CONCAT(a.nombre, ' · ', l.nombre), l.anio_lectivo_id
        FROM lapsos l JOIN anios_lectivos a ON a.id = l.anio_lectivo_id
        WHERE (@anio IS NULL OR a.id = @anio)
          AND (@seccion IS NULL OR EXISTS (SELECT 1 FROM secciones s WHERE s.anio_lectivo_id = a.id AND s.id = @seccion))
          AND (@docente IS NULL OR EXISTS (SELECT 1 FROM secciones s JOIN asignaciones_docentes ad ON ad.seccion_id = s.id
              JOIN docentes d ON d.id = ad.docente_id WHERE s.anio_lectivo_id = a.id AND ad.docente_id = @docente
              AND d.activo = 1 AND (@seccion IS NULL OR s.id = @seccion)))
        ORDER BY a.inicio DESC, l.orden, l.id
        """;
    private const string Estudiantes = """
        SELECT e.id, CONCAT(e.apellidos, ', ', e.nombres, IF(e.identificacion IS NULL, '', CONCAT(' · ', CONVERT(e.identificacion USING utf8mb4) COLLATE utf8mb4_unicode_520_ci))), NULL
        FROM estudiantes e WHERE e.activo = 1 AND @docente IS NULL
          AND ((@anio IS NULL AND @seccion IS NULL) OR EXISTS (SELECT 1 FROM inscripciones i
              WHERE i.estudiante_id = e.id AND i.estado = 1 AND (@anio IS NULL OR i.anio_lectivo_id = @anio)
              AND (@seccion IS NULL OR i.seccion_id = @seccion)))
        ORDER BY e.apellidos, e.nombres, e.id
        """;
    private const string Docentes = """
        SELECT d.id, CONCAT(d.apellidos, ', ', d.nombres), NULL FROM docentes d WHERE d.activo = 1 AND @docente IS NULL
          AND ((@anio IS NULL AND @seccion IS NULL) OR EXISTS (SELECT 1 FROM asignaciones_docentes ad
              JOIN secciones s ON s.id = ad.seccion_id WHERE ad.docente_id = d.id
              AND (@anio IS NULL OR s.anio_lectivo_id = @anio) AND (@seccion IS NULL OR s.id = @seccion)))
        ORDER BY d.apellidos, d.nombres, d.id
        """;
    private const string Materias = """
        SELECT m.id, CONCAT(CONVERT(m.codigo USING utf8mb4) COLLATE utf8mb4_unicode_520_ci, ' · ', m.nombre), NULL FROM materias m
        WHERE (m.activa = 1 OR @docente IS NOT NULL)
          AND ((@docente IS NULL AND @anio IS NULL AND @seccion IS NULL) OR EXISTS (
              SELECT 1 FROM asignaciones_docentes ad JOIN secciones s ON s.id = ad.seccion_id JOIN docentes d ON d.id = ad.docente_id
              WHERE ad.materia_id = m.id AND (@anio IS NULL OR s.anio_lectivo_id = @anio) AND (@seccion IS NULL OR s.id = @seccion)
              AND (@docente IS NULL OR (ad.docente_id = @docente AND d.activo = 1))))
        ORDER BY m.codigo, m.id
        """;
    public async Task<IReadOnlyList<OpcionAcademica>> ConsultarAsync(TipoCatalogoAcademico tipo, AlcanceAcademico alcance, Guid? anioId, Guid? seccionId, int limite, CancellationToken ct)
    {
        if (limite is < 1 or > 2000) throw Error.DatosInvalidos("El límite de opciones no es válido.");
        var sql = tipo switch {
            TipoCatalogoAcademico.Anios => alcance.DocenteId is null ? AniosAdministracion : Anios,
            TipoCatalogoAcademico.Secciones => Secciones, TipoCatalogoAcademico.Lapsos => Lapsos,
            TipoCatalogoAcademico.Estudiantes => Estudiantes, TipoCatalogoAcademico.Docentes => Docentes,
            TipoCatalogoAcademico.Materias => Materias, _ => throw Error.DatosInvalidos("El catálogo no existe.")
        };
        return await sesion.PaginaAsync(sql + " LIMIT @limite", f => new OpcionAcademica(f.Id(0), f.GetString(1), f.IsDBNull(2) ? null : f.Id(2)),
            limite + 1, ct, Parametros.IdOpcional("@anio", anioId), Parametros.IdOpcional("@seccion", seccionId),
            Parametros.IdOpcional("@docente", alcance.DocenteId), Parametros.Entero("@limite", limite + 1));
    }
}
