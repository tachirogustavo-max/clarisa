CREATE TABLE IF NOT EXISTS ventanas_carga (
    id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    lapso_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    estado TINYINT NOT NULL,
    abierta_desde_utc_ticks BIGINT NOT NULL,
    hasta_utc_ticks BIGINT NULL,
    cerrada_en_utc_ticks BIGINT NULL,
    version BIGINT NOT NULL,
    CONSTRAINT uq_ventana_lapso UNIQUE (lapso_id),
    CONSTRAINT uq_ventana_id_lapso UNIQUE (id, lapso_id),
    CONSTRAINT fk_ventana_lapso FOREIGN KEY (lapso_id) REFERENCES lapsos (id),
    CONSTRAINT ck_ventana_estado CHECK ((estado = 1 AND cerrada_en_utc_ticks IS NOT NULL AND cerrada_en_utc_ticks >= abierta_desde_utc_ticks) OR (estado = 2 AND cerrada_en_utc_ticks IS NULL)),
    CONSTRAINT ck_ventana_plazo CHECK (hasta_utc_ticks IS NULL OR hasta_utc_ticks > abierta_desde_utc_ticks),
    CONSTRAINT ck_ventana_version CHECK (version >= 1)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

CREATE TABLE IF NOT EXISTS calificaciones (
    id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    inscripcion_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    materia_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    lapso_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    anio_lectivo_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    valor DECIMAL(4,2) NULL,
    version BIGINT NOT NULL,
    CONSTRAINT uq_calificacion_triple UNIQUE (inscripcion_id, materia_id, lapso_id),
    CONSTRAINT uq_calificacion_contexto UNIQUE (id, inscripcion_id, materia_id, lapso_id),
    CONSTRAINT fk_calificacion_inscripcion_anio FOREIGN KEY (inscripcion_id, anio_lectivo_id) REFERENCES inscripciones (id, anio_lectivo_id),
    CONSTRAINT fk_calificacion_lapso_anio FOREIGN KEY (lapso_id, anio_lectivo_id) REFERENCES lapsos (id, anio_lectivo_id),
    CONSTRAINT fk_calificacion_materia FOREIGN KEY (materia_id) REFERENCES materias (id),
    CONSTRAINT ck_calificacion_valor CHECK (valor IS NULL OR valor BETWEEN 1 AND 20),
    CONSTRAINT ck_calificacion_version CHECK (version >= 1),
    INDEX ix_calificacion_planilla (lapso_id, materia_id, inscripcion_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

CREATE TABLE IF NOT EXISTS auditoria (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    actor_usuario_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    accion TINYINT NOT NULL,
    entidad VARCHAR(80) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    entidad_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    ocurrido_utc_ticks BIGINT NOT NULL,
    CONSTRAINT ck_auditoria_accion CHECK (accion BETWEEN 1 AND 11),
    INDEX ix_auditoria_entidad (entidad, entidad_id, ocurrido_utc_ticks, id),
    INDEX ix_auditoria_actor (actor_usuario_id, ocurrido_utc_ticks, id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

CREATE TABLE IF NOT EXISTS movimientos_ventana_carga (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    ventana_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    lapso_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    accion TINYINT NOT NULL,
    hasta_utc_ticks BIGINT NULL,
    version_resultante BIGINT NOT NULL,
    actor_usuario_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    ocurrido_utc_ticks BIGINT NOT NULL,
    CONSTRAINT fk_movimiento_ventana_lapso FOREIGN KEY (ventana_id, lapso_id) REFERENCES ventanas_carga (id, lapso_id),
    CONSTRAINT ck_movimiento_accion CHECK (accion BETWEEN 1 AND 3),
    CONSTRAINT ck_movimiento_version CHECK (version_resultante >= 1),
    CONSTRAINT uq_movimiento_version UNIQUE (ventana_id, version_resultante),
    INDEX ix_movimiento_lapso_fecha (lapso_id, ocurrido_utc_ticks, id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

CREATE TABLE IF NOT EXISTS cambios_calificacion (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    calificacion_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    inscripcion_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    materia_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    lapso_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    anterior DECIMAL(4,2) NULL,
    nuevo DECIMAL(4,2) NULL,
    version_resultante BIGINT NOT NULL,
    actor_usuario_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    ocurrido_utc_ticks BIGINT NOT NULL,
    CONSTRAINT fk_cambio_calificacion_contexto FOREIGN KEY (calificacion_id, inscripcion_id, materia_id, lapso_id) REFERENCES calificaciones (id, inscripcion_id, materia_id, lapso_id),
    CONSTRAINT ck_cambio_anterior CHECK (anterior IS NULL OR anterior BETWEEN 1 AND 20),
    CONSTRAINT ck_cambio_nuevo CHECK (nuevo IS NULL OR nuevo BETWEEN 1 AND 20),
    CONSTRAINT ck_cambio_version CHECK (version_resultante >= 1),
    CONSTRAINT uq_cambio_calificacion_version UNIQUE (calificacion_id, version_resultante),
    INDEX ix_cambio_inscripcion_fecha (inscripcion_id, ocurrido_utc_ticks, id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

CREATE TRIGGER IF NOT EXISTS tr_auditoria_no_actualizar BEFORE UPDATE ON auditoria FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'La auditoría no se modifica';
CREATE TRIGGER IF NOT EXISTS tr_auditoria_no_borrar BEFORE DELETE ON auditoria FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'La auditoría no se elimina';
CREATE TRIGGER IF NOT EXISTS tr_movimiento_no_actualizar BEFORE UPDATE ON movimientos_ventana_carga FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'El historial de ventanas no se modifica';
CREATE TRIGGER IF NOT EXISTS tr_movimiento_no_borrar BEFORE DELETE ON movimientos_ventana_carga FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'El historial de ventanas no se elimina';
CREATE TRIGGER IF NOT EXISTS tr_cambio_no_actualizar BEFORE UPDATE ON cambios_calificacion FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'El historial de notas no se modifica';
CREATE TRIGGER IF NOT EXISTS tr_cambio_no_borrar BEFORE DELETE ON cambios_calificacion FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'El historial de notas no se elimina';
