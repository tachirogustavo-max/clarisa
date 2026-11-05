namespace Clarisa.Infrastructure.Persistence.Esquema;

// Forma exacta que deben dejar las migraciones. Columnas «nombre:tipo»; «?» tras el nombre = admite nulo; «*» tras el tipo = generada.
// Tipos: id = CHAR(36) ascii_bin; txtN = VARCHAR(N) utf8mb4_unicode_520_ci; claveN = VARCHAR(N) utf8mb4_bin; asciiN = VARCHAR(N) ascii_bin.
// Reemplazada indica la versión en la que una migración posterior cambió la forma de la tabla; esa forma deja de esperarse desde ella.
internal sealed record TablaEsperada(int Version, string Nombre, string Columnas, string Restricciones, string Indices = "",
    int? Reemplazada = null)
{
    public bool VigenteEn(int version) => Version <= version && (Reemplazada is null || version < Reemplazada);
}

internal sealed record DisparadorEsperado(int Version, string Nombre, string Tabla, string Momento, string Evento);

internal static class EsquemaEsperado
{
    public const string TablaVersiones = "esquema_versiones";
    // La marca está escrita en las bases ya instaladas y la comprueban las versiones anteriores: conserva el nombre interno antiguo.
    public const string MarcaPropietario = "escolar-liceo/esquema-versionado";

    public static readonly IReadOnlyList<TablaEsperada> Tablas =
    [
        new(0, TablaVersiones, "version:int nombre:ascii120 huella:ascii64 aplicada_utc_ticks:bigint", "PRIMARY"),
        new(1, "bloqueo_escritura", "id:tinyint", "PRIMARY ck_bloqueo_unico"),
        new(1, "anios_lectivos",
            "id:id nombre:txt40 nombre_clave:clave40 inicio:date fin:date lapso_actual_id?:id version:bigint",
            "PRIMARY uq_anio_nombre fk_anio_lapso_actual ck_anio_fechas ck_anio_version ck_anio_nombre"),
        new(1, "lapsos",
            "id:id anio_lectivo_id:id nombre:txt60 nombre_clave:clave60 orden:tinyint inicio:date fin:date version:bigint",
            "PRIMARY uq_lapso_orden uq_lapso_nombre uq_lapso_anio_id uq_lapso_id_anio fk_lapso_anio ck_lapso_orden ck_lapso_fechas ck_lapso_version"),
        new(1, "estudiantes",
            "id:id identificacion?:ascii30 nombres:txt80 apellidos:txt80 fecha_nacimiento?:date activo:tinyint version:bigint",
            "PRIMARY uq_estudiante_identificacion ck_estudiante_activo ck_estudiante_version ck_estudiante_nombres",
            "ix_estudiante_nombre"),
        new(1, "docentes",
            "id:id identificacion:ascii30 nombres:txt80 apellidos:txt80 correo?:txt254 activo:tinyint version:bigint",
            "PRIMARY uq_docente_identificacion ck_docente_activo ck_docente_version ck_docente_nombres",
            "ix_docente_nombre"),
        new(1, "materias",
            "id:id codigo:ascii20 nombre:txt100 activa:tinyint version:bigint",
            "PRIMARY uq_materia_codigo ck_materia_activa ck_materia_version ck_materia_nombre"),
        new(1, "secciones",
            "id:id anio_lectivo_id:id grado:txt40 grado_clave:clave40 nombre:txt20 nombre_clave:clave20 version:bigint",
            "PRIMARY uq_seccion_grado_nombre uq_seccion_id_anio fk_seccion_anio ck_seccion_version"),
        new(1, "inscripciones",
            "id:id estudiante_id:id seccion_id:id anio_lectivo_id:id fecha:date estado:tinyint fecha_retiro?:date version:bigint "
            + "activa_marca?:tinyint*",
            "PRIMARY uq_inscripcion_activa uq_inscripcion_id_anio fk_inscripcion_estudiante fk_inscripcion_seccion_anio "
            + "ck_inscripcion_estado ck_inscripcion_version",
            "ix_inscripcion_seccion_estado"),
        new(1, "asignaciones_docentes",
            "id:id seccion_id:id materia_id:id docente_id:id version:bigint",
            "PRIMARY uq_asignacion_triple fk_asignacion_seccion fk_asignacion_materia fk_asignacion_docente ck_asignacion_version",
            "ix_asignacion_docente"),
        new(2, "ventanas_carga",
            "id:id lapso_id:id estado:tinyint abierta_desde_utc_ticks:bigint hasta_utc_ticks?:bigint cerrada_en_utc_ticks?:bigint "
            + "version:bigint",
            "PRIMARY uq_ventana_lapso uq_ventana_id_lapso fk_ventana_lapso ck_ventana_estado ck_ventana_plazo ck_ventana_version"),
        new(2, "calificaciones",
            "id:id inscripcion_id:id materia_id:id lapso_id:id anio_lectivo_id:id valor?:decimal(4,2) version:bigint",
            "PRIMARY uq_calificacion_triple uq_calificacion_contexto fk_calificacion_inscripcion_anio fk_calificacion_lapso_anio "
            + "fk_calificacion_materia ck_calificacion_valor ck_calificacion_version",
            "ix_calificacion_planilla", Reemplazada: 4),
        new(2, "auditoria",
            "id:bigint actor_usuario_id:id accion:tinyint entidad:ascii80 entidad_id:id ocurrido_utc_ticks:bigint",
            "PRIMARY ck_auditoria_accion",
            "ix_auditoria_entidad ix_auditoria_actor"),
        new(2, "movimientos_ventana_carga",
            "id:bigint ventana_id:id lapso_id:id accion:tinyint hasta_utc_ticks?:bigint version_resultante:bigint actor_usuario_id:id "
            + "ocurrido_utc_ticks:bigint",
            "PRIMARY fk_movimiento_ventana_lapso ck_movimiento_accion ck_movimiento_version uq_movimiento_version",
            "ix_movimiento_lapso_fecha"),
        new(2, "cambios_calificacion",
            "id:bigint calificacion_id:id inscripcion_id:id materia_id:id lapso_id:id anterior?:decimal(4,2) nuevo?:decimal(4,2) "
            + "version_resultante:bigint actor_usuario_id:id ocurrido_utc_ticks:bigint",
            "PRIMARY fk_cambio_calificacion_contexto ck_cambio_anterior ck_cambio_nuevo ck_cambio_version uq_cambio_calificacion_version",
            "ix_cambio_inscripcion_fecha", Reemplazada: 4),
        new(3, "cuentas_usuario",
            "id:id usuario:ascii40 nombre:txt80 perfil:tinyint docente_id?:id huella_clave:ascii200 activo:tinyint version:bigint",
            "PRIMARY uq_cuenta_usuario uq_cuenta_docente fk_cuenta_docente ck_cuenta_perfil ck_cuenta_usuario ck_cuenta_nombre "
            + "ck_cuenta_huella ck_cuenta_activo ck_cuenta_version"),
        new(3, "sesiones_usuario",
            "id:id cuenta_id:id huella_token:ascii64 creada_utc_ticks:bigint caduca_utc_ticks:bigint revocada_utc_ticks?:bigint "
            + "motivo_revocacion?:tinyint",
            "PRIMARY uq_sesion_huella fk_sesion_cuenta ck_sesion_huella ck_sesion_plazo ck_sesion_revocacion",
            "ix_sesion_cuenta"),
        new(3, "auditoria_cuentas",
            "id:bigint actor_usuario_id:id accion:tinyint cuenta_id:id ocurrido_utc_ticks:bigint",
            "PRIMARY fk_auditoria_cuenta ck_auditoria_cuenta_accion",
            "ix_auditoria_cuenta_fecha ix_auditoria_cuenta_actor"),
        new(4, "escala_cualitativa",
            "id:id anio_lectivo_id:id letra:ascii4 descripcion?:txt100 version:bigint",
            "PRIMARY uq_escala_letra fk_escala_anio ck_escala_letra ck_escala_descripcion ck_escala_version"),
        new(4, "plan_materias",
            "id:id anio_lectivo_id:id grado:txt40 grado_clave:clave40 materia_id:id modalidad:tinyint version:bigint",
            "PRIMARY uq_plan_grado_materia fk_plan_anio fk_plan_materia ck_plan_modalidad ck_plan_grado ck_plan_version"),
        new(4, "calificaciones",
            "id:id inscripcion_id:id materia_id:id lapso_id:id anio_lectivo_id:id valor?:decimal(4,2) tipo:tinyint letra?:ascii4 "
            + "motivo_exoneracion?:txt200 version:bigint",
            "PRIMARY uq_calificacion_triple uq_calificacion_contexto fk_calificacion_inscripcion_anio fk_calificacion_lapso_anio "
            + "fk_calificacion_materia ck_calificacion_valor ck_calificacion_version ck_calificacion_resultado fk_calificacion_letra",
            "ix_calificacion_planilla", Reemplazada: 8),
        new(4, "cambios_calificacion",
            "id:bigint calificacion_id:id inscripcion_id:id materia_id:id lapso_id:id anterior?:decimal(4,2) nuevo?:decimal(4,2) "
            + "tipo_anterior?:tinyint tipo_nuevo?:tinyint letra_anterior?:ascii4 letra_nueva?:ascii4 motivo?:txt200 "
            + "version_resultante:bigint actor_usuario_id:id ocurrido_utc_ticks:bigint",
            "PRIMARY fk_cambio_calificacion_contexto ck_cambio_anterior ck_cambio_nuevo ck_cambio_version uq_cambio_calificacion_version "
            + "ck_cambio_resultado",
            "ix_cambio_inscripcion_fecha", Reemplazada: 8),
        new(5, "recuperaciones_cuenta",
            "id:id cuenta_id:id version_resultante:bigint identidad_windows:txt184 usuario_base:txt80 motivo:txt200 ocurrido_utc_ticks:bigint",
            "PRIMARY fk_recuperacion_cuenta uq_recuperacion_cuenta_version ck_recuperacion_version ck_recuperacion_identidad ck_recuperacion_motivo",
            "ix_recuperacion_cuenta_fecha"),
        new(6, "politicas_academicas",
            "id:id anio_lectivo_id:id numero:int estado:tinyint decimales_captura?:tinyint decimales_resultado?:tinyint "
            + "nota_aprobatoria?:decimal(4,2) formula?:tinyint redondeo?:tinyint tratamiento_exonerado?:tinyint peso_lapso1?:decimal(5,2) "
            + "peso_lapso2?:decimal(5,2) peso_lapso3?:decimal(5,2) motivo:txt200 creada_por:id creada_utc_ticks:bigint activada_por?:id "
            + "activada_utc_ticks?:bigint motivo_activacion?:txt200 version:bigint estado_unico?:tinyint*",
            "PRIMARY uq_politica_numero uq_politica_estado uq_politica_id_anio fk_politica_anio ck_politica_estado ck_politica_numero "
            + "ck_politica_decimales ck_politica_aprobatoria ck_politica_opciones ck_politica_pesos ck_politica_completa ck_politica_activacion "
            + "ck_politica_motivo ck_politica_version"),
        new(6, "cierres_resultados",
            "id:id anio_lectivo_id:id seccion_id:id numero:int politica_id:id estado:tinyint cerrado_por:id cerrado_utc_ticks:bigint "
            + "anulado_por?:id anulado_utc_ticks?:bigint motivo_anulacion?:txt200 version:bigint vigente_marca?:tinyint*",
            "PRIMARY uq_cierre_numero uq_cierre_vigente fk_cierre_seccion_anio fk_cierre_politica_anio ck_cierre_estado ck_cierre_numero "
            + "ck_cierre_anulacion ck_cierre_version"),
        new(6, "resultados_cerrados",
            "id:bigint cierre_id:id inscripcion_id:id materia_id:id modalidad?:tinyint tipo_lapso1:tinyint valor_lapso1?:decimal(4,2) "
            + "letra_lapso1?:ascii4 version_lapso1:bigint tipo_lapso2:tinyint valor_lapso2?:decimal(4,2) letra_lapso2?:ascii4 "
            + "version_lapso2:bigint tipo_lapso3:tinyint valor_lapso3?:decimal(4,2) letra_lapso3?:ascii4 version_lapso3:bigint "
            + "estado_definitiva:tinyint valor_definitiva?:decimal(4,2)",
            "PRIMARY uq_resultado_cerrado fk_resultado_cierre fk_resultado_inscripcion fk_resultado_materia ck_resultado_modalidad "
            + "ck_resultado_lapso1 ck_resultado_lapso2 ck_resultado_lapso3 ck_resultado_definitiva",
            "ix_resultado_inscripcion"),
        new(7, "firmantes_boletin",
            "id:id numero:int director?:txt120 coordinador_evaluacion?:txt120 coordinador_estudios?:txt120 motivo:txt200 registrado_por:id "
            + "registrado_utc_ticks:bigint",
            "PRIMARY uq_firmantes_numero ck_firmantes_numero ck_firmantes_nombres ck_firmantes_motivo"),
        new(8, "calificaciones",
            "id:id inscripcion_id:id materia_id:id lapso_id:id anio_lectivo_id:id valor?:decimal(4,2) tipo:tinyint letra?:ascii4 "
            + "motivo_exoneracion?:txt200 version:bigint inasistencias?:smallint",
            "PRIMARY uq_calificacion_triple uq_calificacion_contexto fk_calificacion_inscripcion_anio fk_calificacion_lapso_anio "
            + "fk_calificacion_materia ck_calificacion_valor ck_calificacion_version ck_calificacion_resultado fk_calificacion_letra "
            + "ck_calificacion_inasistencias",
            "ix_calificacion_planilla"),
        new(8, "cambios_calificacion",
            "id:bigint calificacion_id:id inscripcion_id:id materia_id:id lapso_id:id anterior?:decimal(4,2) nuevo?:decimal(4,2) "
            + "tipo_anterior?:tinyint tipo_nuevo?:tinyint letra_anterior?:ascii4 letra_nueva?:ascii4 motivo?:txt200 "
            + "version_resultante:bigint actor_usuario_id:id ocurrido_utc_ticks:bigint inasistencias_anteriores?:smallint "
            + "inasistencias_nuevas?:smallint",
            "PRIMARY fk_cambio_calificacion_contexto ck_cambio_anterior ck_cambio_nuevo ck_cambio_version uq_cambio_calificacion_version "
            + "ck_cambio_resultado ck_cambio_inasistencias",
            "ix_cambio_inscripcion_fecha"),
        new(9, "grados", "id:id nombre:txt40 nombre_clave:clave40 orden:smallint activo:tinyint version:bigint",
            "PRIMARY uq_grado_nombre ck_grado_orden ck_grado_activo ck_grado_version ck_grado_nombre")
    ];

    public static readonly IReadOnlyList<DisparadorEsperado> Disparadores =
    [
        new(2, "tr_auditoria_no_actualizar", "auditoria", "BEFORE", "UPDATE"),
        new(2, "tr_auditoria_no_borrar", "auditoria", "BEFORE", "DELETE"),
        new(2, "tr_movimiento_no_actualizar", "movimientos_ventana_carga", "BEFORE", "UPDATE"),
        new(2, "tr_movimiento_no_borrar", "movimientos_ventana_carga", "BEFORE", "DELETE"),
        new(2, "tr_cambio_no_actualizar", "cambios_calificacion", "BEFORE", "UPDATE"),
        new(2, "tr_cambio_no_borrar", "cambios_calificacion", "BEFORE", "DELETE"),
        new(3, "tr_auditoria_cuenta_no_actualizar", "auditoria_cuentas", "BEFORE", "UPDATE"),
        new(3, "tr_auditoria_cuenta_no_borrar", "auditoria_cuentas", "BEFORE", "DELETE"),
        new(5, "tr_recuperacion_cuenta_no_actualizar", "recuperaciones_cuenta", "BEFORE", "UPDATE"),
        new(5, "tr_recuperacion_cuenta_no_borrar", "recuperaciones_cuenta", "BEFORE", "DELETE"),
        new(6, "tr_resultado_cerrado_no_actualizar", "resultados_cerrados", "BEFORE", "UPDATE"),
        new(6, "tr_resultado_cerrado_no_borrar", "resultados_cerrados", "BEFORE", "DELETE"),
        new(7, "tr_firmantes_no_actualizar", "firmantes_boletin", "BEFORE", "UPDATE"),
        new(7, "tr_firmantes_no_borrar", "firmantes_boletin", "BEFORE", "DELETE")
    ];

    public static IEnumerable<string> ColumnasNormalizadas(TablaEsperada tabla) =>
        tabla.Columnas.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(columna =>
        {
            var (nombre, tipo) = (columna[..columna.IndexOf(':')], columna[(columna.IndexOf(':') + 1)..]);
            var generada = tipo.EndsWith('*');
            return $"{nombre}:{ExpandirTipo(tipo.TrimEnd('*'))}{(generada ? "*" : "")}";
        });

    private static string ExpandirTipo(string tipo) => tipo switch
    {
        "id" => "char(36) ascii_bin",
        _ when tipo.StartsWith("txt", StringComparison.Ordinal) => $"varchar({tipo[3..]}) utf8mb4_unicode_520_ci",
        _ when tipo.StartsWith("clave", StringComparison.Ordinal) => $"varchar({tipo[5..]}) utf8mb4_bin",
        _ when tipo.StartsWith("ascii", StringComparison.Ordinal) => $"varchar({tipo[5..]}) ascii_bin",
        _ => tipo
    };
}
