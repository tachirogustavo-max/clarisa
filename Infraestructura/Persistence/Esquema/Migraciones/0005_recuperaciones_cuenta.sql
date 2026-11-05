CREATE TABLE IF NOT EXISTS recuperaciones_cuenta (
    id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    cuenta_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    version_resultante BIGINT NOT NULL,
    identidad_windows VARCHAR(184) NOT NULL,
    usuario_base VARCHAR(80) NOT NULL,
    motivo VARCHAR(200) NOT NULL,
    ocurrido_utc_ticks BIGINT NOT NULL,
    CONSTRAINT fk_recuperacion_cuenta FOREIGN KEY (cuenta_id) REFERENCES cuentas_usuario (id),
    CONSTRAINT uq_recuperacion_cuenta_version UNIQUE (cuenta_id, version_resultante),
    CONSTRAINT ck_recuperacion_version CHECK (version_resultante >= 2),
    CONSTRAINT ck_recuperacion_identidad CHECK (CHAR_LENGTH(TRIM(identidad_windows)) > 0 AND CHAR_LENGTH(TRIM(usuario_base)) > 0),
    CONSTRAINT ck_recuperacion_motivo CHECK (CHAR_LENGTH(TRIM(motivo)) > 0),
    INDEX ix_recuperacion_cuenta_fecha (cuenta_id, ocurrido_utc_ticks, id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

CREATE TRIGGER IF NOT EXISTS tr_recuperacion_cuenta_no_actualizar BEFORE UPDATE ON recuperaciones_cuenta FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'La recuperación de cuentas no se modifica';
CREATE TRIGGER IF NOT EXISTS tr_recuperacion_cuenta_no_borrar BEFORE DELETE ON recuperaciones_cuenta FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'La recuperación de cuentas no se elimina';
