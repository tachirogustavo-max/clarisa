using Clarisa.Application.Common.Consultas;
using Clarisa.Application.Features.LecturasAcademicas;
using Clarisa.Domain.Entities;
using Clarisa.Infrastructure.Persistence.Sesion;

namespace Clarisa.Infrastructure.Persistence.Lecturas;

internal sealed class ConsultaRelacionesMariaDb(SesionMariaDb sesion)
{
    private const string DesdeInscripciones = """
         FROM inscripciones i JOIN estudiantes e ON e.id = i.estudiante_id
         JOIN secciones s ON s.id = i.seccion_id
         WHERE (@anio IS NULL OR s.anio_lectivo_id = @anio) AND (@seccion IS NULL OR s.id = @seccion)
           AND (@patron IS NULL OR CONCAT(e.apellidos, ' ', e.nombres) LIKE @patron ESCAPE '!'
                OR CONVERT(e.identificacion USING utf8mb4) COLLATE utf8mb4_unicode_520_ci LIKE @patron ESCAPE '!'
                OR s.nombre LIKE @patron ESCAPE '!' OR s.grado LIKE @patron ESCAPE '!')
        """;
    private const string DesdeAsignaciones = """
         FROM asignaciones_docentes ad JOIN secciones s ON s.id = ad.seccion_id
         JOIN materias m ON m.id = ad.materia_id JOIN docentes d ON d.id = ad.docente_id
         WHERE (@anio IS NULL OR s.anio_lectivo_id = @anio) AND (@seccion IS NULL OR s.id = @seccion)
           AND (@docente IS NULL OR (ad.docente_id = @docente AND d.activo = 1))
           AND (@patron IS NULL OR CONCAT(d.apellidos, ' ', d.nombres) LIKE @patron ESCAPE '!'
                OR m.nombre LIKE @patron ESCAPE '!' OR s.nombre LIKE @patron ESCAPE '!' OR s.grado LIKE @patron ESCAPE '!')
        """;
    public async Task<Pagina<InscripcionVista>> InscripcionesAsync(FiltroLecturas filtro, ConsultaPagina consulta, CancellationToken ct)
    {
        consulta = ConsultaPagina.Validar(consulta);
        var condiciones = Condiciones(filtro);
        var total = await sesion.EnteroAsync("SELECT COUNT(*)" + DesdeInscripciones, ct, condiciones);
        var filas = await sesion.PaginaAsync("""
            SELECT i.id, i.estudiante_id, i.seccion_id, CONCAT(e.apellidos, ', ', e.nombres), e.identificacion,
                CONCAT(s.grado, ' · ', s.nombre), i.fecha, i.estado, i.fecha_retiro, i.version
            """ + DesdeInscripciones + " ORDER BY e.apellidos, e.nombres, i.id LIMIT @tamanio OFFSET @desde",
            f => new InscripcionVista(f.Id(0), f.Id(1), f.Id(2), f.GetString(3), f.TextoOpcional(4), f.GetString(5),
                f.Fecha(6), f.Enumerado<EstadoInscripcion>(7).ToString(), f.FechaOpcional(8), f.GetInt64(9)), consulta.Tamanio, ct,
            [.. condiciones, Parametros.Entero("@tamanio", consulta.Tamanio), Parametros.Entero("@desde", consulta.Desplazamiento)]);
        return new(filas, consulta.Numero, consulta.Tamanio, checked((int)total));
    }
    public async Task<Pagina<AsignacionVista>> AsignacionesAsync(AlcanceAcademico alcance, FiltroLecturas filtro, ConsultaPagina consulta, CancellationToken ct)
    {
        consulta = ConsultaPagina.Validar(consulta);
        Parametro[] condiciones = [.. Condiciones(filtro), Parametros.IdOpcional("@docente", alcance.DocenteId)];
        var total = await sesion.EnteroAsync("SELECT COUNT(*)" + DesdeAsignaciones, ct, condiciones);
        var filas = await sesion.PaginaAsync("""
            SELECT ad.id, ad.seccion_id, ad.materia_id, ad.docente_id, CONCAT(s.grado, ' · ', s.nombre),
                m.nombre, CONCAT(d.apellidos, ', ', d.nombres), ad.version
            """ + DesdeAsignaciones + " ORDER BY s.grado, s.nombre, m.nombre, d.apellidos, d.nombres, ad.id LIMIT @tamanio OFFSET @desde",
            f => new AsignacionVista(f.Id(0), f.Id(1), f.Id(2), f.Id(3), f.GetString(4), f.GetString(5), f.GetString(6), f.GetInt64(7)),
            consulta.Tamanio, ct, [.. condiciones, Parametros.Entero("@tamanio", consulta.Tamanio), Parametros.Entero("@desde", consulta.Desplazamiento)]);
        return new(filas, consulta.Numero, consulta.Tamanio, checked((int)total));
    }
    private static Parametro[] Condiciones(FiltroLecturas filtro) => [
        Parametros.IdOpcional("@anio", filtro.AnioLectivoId), Parametros.IdOpcional("@seccion", filtro.SeccionId),
        filtro.Texto is null ? Parametros.Texto("@patron", null) : Parametros.Patron("@patron", filtro.Texto)
    ];
}
