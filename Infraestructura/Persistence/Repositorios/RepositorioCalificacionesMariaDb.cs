using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Application.Common.Errores;
using Clarisa.Domain.Common;
using Clarisa.Domain.Entities;
using Clarisa.Infrastructure.Persistence.Sesion;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Repositorios;

internal sealed class RepositorioCalificacionesMariaDb(SesionMariaDb sesion) : IRepositorioCalificaciones
{
    // Tres lapsos por cada materia de cada inscripción consultada: el límite de filas es mayor que el de listas simples.
    public const int MaximoFilasPorInscripciones = 20_000;
    public Task<bool> ExistenEnSeccionAsync(Guid seccionId, CancellationToken ct) => sesion.ExisteAsync("""
        SELECT 1 FROM calificaciones c JOIN inscripciones i ON i.id = c.inscripcion_id WHERE i.seccion_id = @seccion
        """, ct, Parametros.Id("@seccion", seccionId));

    private const string Columnas =
        "SELECT id, inscripcion_id, materia_id, lapso_id, tipo, valor, letra, motivo_exoneracion, version, inasistencias FROM calificaciones";

    public Task<IReadOnlyList<Calificacion>> ListarAsync(Guid lapsoId, Guid materiaId, IReadOnlyCollection<Guid> inscripcionIds,
        CancellationToken ct) =>
        ConsultaPorIds.ListarAsync(sesion, Columnas + " WHERE lapso_id = @lapso AND materia_id = @materia AND inscripcion_id IN ({0})",
            inscripcionIds, Convertir, ct, SesionMariaDb.LimiteListas, Parametros.Id("@lapso", lapsoId), Parametros.Id("@materia", materiaId));

    public Task<IReadOnlyList<Calificacion>> ListarPorInscripcionesAsync(IReadOnlyCollection<Guid> inscripcionIds, CancellationToken ct) =>
        ConsultaPorIds.ListarAsync(sesion, Columnas + " WHERE inscripcion_id IN ({0})", inscripcionIds, Convertir, ct,
            MaximoFilasPorInscripciones);

    public Task<bool> ExistenEnSeccionMateriaAsync(Guid seccionId, Guid materiaId, CancellationToken ct) =>
        sesion.ExisteAsync("""
            SELECT 1 FROM calificaciones c JOIN inscripciones i ON i.id = c.inscripcion_id
            WHERE i.seccion_id = @seccion AND c.materia_id = @materia
            """, ct, Parametros.Id("@seccion", seccionId), Parametros.Id("@materia", materiaId));

    public async Task<IReadOnlyDictionary<TipoResultado, int>> ContarPorTipoEnGradoAsync(Guid anioLectivoId, string grado, Guid materiaId,
        CancellationToken ct)
    {
        var filas = await sesion.ListarAsync("""
            SELECT c.tipo, COUNT(*) FROM calificaciones c
            JOIN inscripciones i ON i.id = c.inscripcion_id
            JOIN secciones s ON s.id = i.seccion_id
            WHERE c.anio_lectivo_id = @anio AND c.materia_id = @materia AND s.grado_clave = @grado
            GROUP BY c.tipo
            """, fila => (Tipo: fila.Enumerado<TipoResultado>(0), Cantidad: fila.GetInt32(1)), Enum.GetValues<TipoResultado>().Length, ct,
            Parametros.Id("@anio", anioLectivoId), Parametros.Id("@materia", materiaId), Parametros.Texto("@grado", Textos.Clave(grado)));
        return filas.ToDictionary(fila => fila.Tipo, fila => fila.Cantidad);
    }

    public Task<bool> ExistenConMasDecimalesAsync(Guid anioLectivoId, int decimales, CancellationToken ct) =>
        sesion.ExisteAsync("SELECT 1 FROM calificaciones WHERE anio_lectivo_id = @anio AND valor IS NOT NULL AND valor <> TRUNCATE(valor, @decimales)",
            ct, Parametros.Id("@anio", anioLectivoId), Parametros.Entero("@decimales", decimales));

    // El año se toma de la inscripción dentro del mismo INSERT; las claves foráneas compuestas impiden un lapso de otro año.
    public async Task AgregarAsync(Calificacion calificacion, CancellationToken ct)
    {
        var filas = await sesion.EjecutarAsync("""
            INSERT INTO calificaciones (id, inscripcion_id, materia_id, lapso_id, anio_lectivo_id, valor, tipo, letra, motivo_exoneracion,
                version, inasistencias)
            SELECT @id, i.id, @materia, @lapso, i.anio_lectivo_id, @valor, @tipo, @letra, @motivo, @version, @inasistencias
            FROM inscripciones i WHERE i.id = @inscripcion
            """, ct, Datos(calificacion));
        if (filas != 1)
        {
            sesion.Marcar(EstadoSesion.Fallida);
            throw Error.ReferenciaInvalida("La inscripción no existe.", "InscripcionId");
        }
    }

    public async Task<long> ActualizarAsync(Calificacion calificacion, CancellationToken ct)
    {
        var filas = await sesion.EjecutarAsync("""
            UPDATE calificaciones SET valor = @valor, tipo = @tipo, letra = @letra, motivo_exoneracion = @motivo,
                inasistencias = @inasistencias, version = version + 1
            WHERE id = @id AND inscripcion_id = @inscripcion AND materia_id = @materia AND lapso_id = @lapso AND version = @version
            """, ct, Datos(calificacion));
        await sesion.ExigirUnaFilaAsync(filas, "calificaciones", calificacion.Id, ct);
        return calificacion.Version + 1;
    }

    public Task RegistrarCambioAsync(CambioCalificacion cambio, CancellationToken ct) =>
        sesion.EjecutarAsync("""
            INSERT INTO cambios_calificacion (calificacion_id, inscripcion_id, materia_id, lapso_id, anterior, nuevo, tipo_anterior,
                tipo_nuevo, letra_anterior, letra_nueva, motivo, version_resultante, actor_usuario_id, ocurrido_utc_ticks,
                inasistencias_anteriores, inasistencias_nuevas)
            VALUES (@calificacion, @inscripcion, @materia, @lapso, @anterior, @nuevo, @tipo_anterior, @tipo_nuevo, @letra_anterior,
                @letra_nueva, @motivo, @version, @actor, @ocurrido, @inasistencias_anteriores, @inasistencias_nuevas)
            """, ct,
            Parametros.Id("@calificacion", cambio.CalificacionId),
            Parametros.Id("@inscripcion", cambio.InscripcionId),
            Parametros.Id("@materia", cambio.MateriaId),
            Parametros.Id("@lapso", cambio.LapsoId),
            Parametros.Nota("@anterior", cambio.Anterior),
            Parametros.Nota("@nuevo", cambio.Nuevo),
            Parametros.EnteroOpcional("@tipo_anterior", (int?)cambio.TipoAnterior),
            Parametros.EnteroOpcional("@tipo_nuevo", (int?)cambio.TipoNuevo),
            Parametros.Texto("@letra_anterior", cambio.LetraAnterior),
            Parametros.Texto("@letra_nueva", cambio.LetraNueva),
            Parametros.Texto("@motivo", cambio.Motivo),
            Parametros.Largo("@version", cambio.VersionResultante),
            Parametros.Id("@actor", cambio.ActorUsuarioId),
            Parametros.Instante("@ocurrido", cambio.OcurridoUtc),
            Parametros.EnteroOpcional("@inasistencias_anteriores", cambio.InasistenciasAnteriores),
            Parametros.EnteroOpcional("@inasistencias_nuevas", cambio.InasistenciasNuevas));

    private static Parametro[] Datos(Calificacion calificacion) =>
    [
        Parametros.Id("@id", calificacion.Id),
        Parametros.Id("@inscripcion", calificacion.InscripcionId),
        Parametros.Id("@materia", calificacion.MateriaId),
        Parametros.Id("@lapso", calificacion.LapsoId),
        Parametros.Nota("@valor", calificacion.Valor),
        Parametros.Entero("@tipo", (int)calificacion.Tipo),
        Parametros.Texto("@letra", calificacion.Letra),
        Parametros.Texto("@motivo", calificacion.MotivoExoneracion),
        Parametros.Largo("@version", calificacion.Version),
        Parametros.EnteroOpcional("@inasistencias", calificacion.Inasistencias)
    ];

    private static Calificacion Convertir(MySqlDataReader fila) =>
        new(fila.Id(0), fila.Id(1), fila.Id(2), fila.Id(3), fila.Enumerado<TipoResultado>(4), fila.Nota(5), fila.TextoOpcional(6),
            fila.TextoOpcional(7), fila.GetInt64(8)) { Inasistencias = fila.IsDBNull(9) ? null : fila.GetInt16(9) };
}
