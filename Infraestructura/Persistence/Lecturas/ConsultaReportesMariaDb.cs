using Clarisa.Application.Common.Consultas;
using Clarisa.Application.Features.LecturasAcademicas;
using Clarisa.Application.Features.Reportes;
using Clarisa.Infrastructure.Persistence.Sesion;

namespace Clarisa.Infrastructure.Persistence.Lecturas;

// STRAIGHT_JOIN fija el orden sección → inscripción → nota. Sin él, MariaDB a veces recorría todas las notas de todos los años.
// Solo la página pedida lee nombres y notas; el total sale de la misma consulta (COUNT OVER).
internal sealed class ConsultaReportesMariaDb(SesionMariaDb sesion)
{
    private sealed record FilaBase(string Estudiante, string? Cedula, string Materia, string Seccion, string Lapso, decimal? Nota,
        Guid SeccionId, Guid MateriaId, string AnioLectivo, long Total, int Tipo, string? Letra);
    private sealed record DocenteMateria(Guid SeccionId, Guid MateriaId, string Nombre);

    private const string Notas = """
         FROM secciones s JOIN inscripciones i ON i.seccion_id = s.id JOIN calificaciones c ON c.inscripcion_id = i.id
        """;
    private const string Filtros = """
         WHERE (@anio IS NULL OR s.anio_lectivo_id = @anio) AND (@seccion IS NULL OR i.seccion_id = @seccion)
           AND (@estudiante IS NULL OR i.estudiante_id = @estudiante) AND (@lapso IS NULL OR c.lapso_id = @lapso)
           AND (@materia IS NULL OR c.materia_id = @materia)
           AND (@actor IS NULL OR EXISTS (SELECT 1 FROM asignaciones_docentes ad JOIN docentes d ON d.id = ad.docente_id
               WHERE ad.seccion_id = i.seccion_id AND ad.materia_id = c.materia_id AND ad.docente_id = @actor AND d.activo = 1))
           AND (@filtroDocente IS NULL OR EXISTS (SELECT 1 FROM asignaciones_docentes ad
               WHERE ad.seccion_id = i.seccion_id AND ad.materia_id = c.materia_id AND ad.docente_id = @filtroDocente))
        """;
    private const string Texto = """
         AND (CONCAT(e.apellidos, ' ', e.nombres) LIKE @patron ESCAPE '!'
               OR CONVERT(e.identificacion USING utf8mb4) COLLATE utf8mb4_unicode_520_ci LIKE @patron ESCAPE '!'
               OR m.nombre LIKE @patron ESCAPE '!' OR s.nombre LIKE @patron ESCAPE '!')
        """;
    private const string ConteoTexto = " JOIN estudiantes e ON e.id = i.estudiante_id JOIN materias m ON m.id = c.materia_id";
    private const string Claves = """
        SELECT STRAIGHT_JOIN c.id, e.orden AS orden_estudiante, so.orden AS orden_seccion, m.codigo, l.orden AS orden_lapso, COUNT(*) OVER () AS total
        """ + Notas + """
         JOIN materias m ON m.id = c.materia_id JOIN lapsos l ON l.id = c.lapso_id
         JOIN (SELECT id, apellidos, nombres, identificacion, DENSE_RANK() OVER (ORDER BY apellidos, nombres) AS orden FROM estudiantes) e
             ON e.id = i.estudiante_id
         JOIN (SELECT id, DENSE_RANK() OVER (ORDER BY grado, nombre) AS orden FROM secciones) so ON so.id = s.id
        """ + Filtros;
    private const string OrdenClaves = " ORDER BY orden_estudiante, orden_seccion, m.codigo, orden_lapso, c.id LIMIT @tamanio OFFSET @desde";
    private const string Presentacion = """
        SELECT STRAIGHT_JOIN CONCAT(e.apellidos, ', ', e.nombres), e.identificacion, m.nombre, CONCAT(s.grado, ' · ', s.nombre),
            l.nombre, c.valor, s.id, m.id, a.nombre, k.total, CASE WHEN c.tipo = 0 AND p.modalidad = 1 THEN 5 ELSE c.tipo END, c.letra
        FROM (
        """;
    private const string PresentacionFin = """
        ) k
        JOIN calificaciones c ON c.id = k.id JOIN inscripciones i ON i.id = c.inscripcion_id JOIN estudiantes e ON e.id = i.estudiante_id
        JOIN secciones s ON s.id = i.seccion_id JOIN materias m ON m.id = c.materia_id JOIN lapsos l ON l.id = c.lapso_id
        JOIN anios_lectivos a ON a.id = c.anio_lectivo_id
        LEFT JOIN plan_materias p ON p.anio_lectivo_id = s.anio_lectivo_id AND p.grado_clave = s.grado_clave AND p.materia_id = m.id
        ORDER BY k.orden_estudiante, k.orden_seccion, k.codigo, k.orden_lapso, k.id
        """;

    internal static string SqlConteo(bool conTexto) =>
        "SELECT STRAIGHT_JOIN COUNT(*)" + Notas + (conTexto ? ConteoTexto + Filtros + Texto : Filtros);

    internal static string SqlPagina(bool conTexto) =>
        Presentacion + Claves + (conTexto ? Texto : "") + OrdenClaves + PresentacionFin;

    public async Task<Pagina<FilaReporte>> ConsultarAsync(AlcanceAcademico alcance, FiltroReporte filtro, ConsultaPagina consulta, CancellationToken ct)
    {
        consulta = ConsultaPagina.Validar(consulta);
        var condiciones = Condiciones(alcance, filtro);
        var conTexto = filtro.Texto is not null;
        var baseFilas = await sesion.PaginaAsync(SqlPagina(conTexto),
            f => new FilaBase(f.GetString(0), f.TextoOpcional(1), f.GetString(2), f.GetString(3), f.GetString(4),
                f.IsDBNull(5) ? null : f.GetDecimal(5), f.Id(6), f.Id(7), f.GetString(8), f.GetInt64(9), f.GetInt32(10), f.TextoOpcional(11)), consulta.Tamanio, ct,
            [.. condiciones, .. Limites(consulta)]);
        var total = baseFilas.Count > 0 ? baseFilas[0].Total
            : consulta.Desplazamiento == 0 ? 0 : await sesion.EnteroAsync(SqlConteo(conTexto), ct, condiciones);
        var docentes = await DocentesPorMateriaAsync(baseFilas, alcance, ct);
        var filas = baseFilas.Select(f => new FilaReporte(f.Estudiante, f.Cedula,
            string.Join("; ", docentes[(f.SeccionId, f.MateriaId)].Select(d => d.Nombre)), f.Materia, f.Seccion, f.Lapso, f.Nota, f.AnioLectivo)
            { Tipo = f.Tipo, Letra = f.Letra }).ToArray();
        return new(filas, consulta.Numero, consulta.Tamanio, checked((int)total));
    }

    internal static Parametro[] Condiciones(AlcanceAcademico alcance, FiltroReporte filtro) =>
    [
        Parametros.IdOpcional("@anio", filtro.AnioLectivoId), Parametros.IdOpcional("@seccion", filtro.SeccionId),
        Parametros.IdOpcional("@estudiante", filtro.EstudianteId), Parametros.IdOpcional("@lapso", filtro.LapsoId),
        Parametros.IdOpcional("@materia", filtro.MateriaId), Parametros.IdOpcional("@actor", alcance.DocenteId),
        Parametros.IdOpcional("@filtroDocente", filtro.DocenteId),
        filtro.Texto is null ? Parametros.Texto("@patron", null) : Parametros.Patron("@patron", filtro.Texto)
    ];

    internal static Parametro[] Limites(ConsultaPagina consulta) =>
        [Parametros.Entero("@tamanio", consulta.Tamanio), Parametros.Entero("@desde", consulta.Desplazamiento)];

    private async Task<ILookup<(Guid, Guid), DocenteMateria>> DocentesPorMateriaAsync(IReadOnlyList<FilaBase> filas, AlcanceAcademico alcance, CancellationToken ct)
    {
        if (filas.Count == 0) return Array.Empty<DocenteMateria>().ToLookup(d => (d.SeccionId, d.MateriaId));
        var pares = filas.Select(f => (f.SeccionId, f.MateriaId)).Distinct().ToArray();
        var parametros = new List<Parametro> { Parametros.IdOpcional("@actor", alcance.DocenteId) };
        var condiciones = new List<string>();
        for (var indice = 0; indice < pares.Length; indice++)
        {
            condiciones.Add($"(@s{indice}, @m{indice})");
            parametros.Add(Parametros.Id("@s" + indice, pares[indice].SeccionId));
            parametros.Add(Parametros.Id("@m" + indice, pares[indice].MateriaId));
        }
        var docentes = await sesion.ListarAsync("""
            SELECT ad.seccion_id, ad.materia_id, CONCAT(d.apellidos, ', ', d.nombres)
            FROM asignaciones_docentes ad JOIN docentes d ON d.id = ad.docente_id
            WHERE (@actor IS NULL OR ad.docente_id = @actor) AND (ad.seccion_id, ad.materia_id) IN (
            """ + string.Join(",", condiciones) + ") ORDER BY d.apellidos, d.nombres, d.id",
            f => new DocenteMateria(f.Id(0), f.Id(1), f.GetString(2)), ct, parametros.ToArray());
        return docentes.ToLookup(d => (d.SeccionId, d.MateriaId));
    }
}
