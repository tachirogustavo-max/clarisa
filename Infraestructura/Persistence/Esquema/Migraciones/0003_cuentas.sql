CREATE TABLE IF NOT EXISTS cuentas_usuario (
    id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    usuario VARCHAR(40) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    nombre VARCHAR(80) NOT NULL,
    perfil TINYINT NOT NULL,
    docente_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NULL,
    huella_clave VARCHAR(200) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    activo TINYINT NOT NULL,
    version BIGINT NOT NULL,
    CONSTRAINT uq_cuenta_usuario UNIQUE (usuario),
    CONSTRAINT uq_cuenta_docente UNIQUE (docente_id),
    CONSTRAINT fk_cuenta_docente FOREIGN KEY (docente_id) REFERENCES docentes (id),
    CONSTRAINT ck_cuenta_perfil CHECK ((perfil = 1 AND docente_id IS NULL) OR (perfil = 2 AND docente_id IS NOT NULL)),
    CONSTRAINT ck_cuenta_usuario CHECK (usuario REGEXP '^[a-z0-9._-]{3,40}$'),
    CONSTRAINT ck_cuenta_nombre CHECK (CHAR_LENGTH(TRIM(nombre)) > 0),
    CONSTRAINT ck_cuenta_huella CHECK (CHAR_LENGTH(huella_clave) > 0),
    CONSTRAINT ck_cuenta_activo CHECK (activo IN (0, 1)),
    CONSTRAINT ck_cuenta_version CHECK (version >= 1)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

CREATE TABLE IF NOT EXISTS sesiones_usuario (
    id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    cuenta_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    huella_token VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    creada_utc_ticks BIGINT NOT NULL,
    caduca_utc_ticks BIGINT NOT NULL,
    revocada_utc_ticks BIGINT NULL,
    motivo_revocacion TINYINT NULL,
    CONSTRAINT uq_sesion_huella UNIQUE (huella_token),
    CONSTRAINT fk_sesion_cuenta FOREIGN KEY (cuenta_id) REFERENCES cuentas_usuario (id),
    CONSTRAINT ck_sesion_huella CHECK (huella_token REGEXP '^[0-9A-F]{64}$'),
    CONSTRAINT ck_sesion_plazo CHECK (caduca_utc_ticks > creada_utc_ticks),
    CONSTRAINT ck_sesion_revocacion CHECK ((revocada_utc_ticks IS NULL AND motivo_revocacion IS NULL) OR (revocada_utc_ticks IS NOT NULL AND motivo_revocacion BETWEEN 1 AND 5)),
    INDEX ix_sesion_cuenta (cuenta_id, revocada_utc_ticks, caduca_utc_ticks)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

CREATE TABLE IF NOT EXISTS auditoria_cuentas (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    actor_usuario_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    accion TINYINT NOT NULL,
    cuenta_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    ocurrido_utc_ticks BIGINT NOT NULL,
    CONSTRAINT fk_auditoria_cuenta FOREIGN KEY (cuenta_id) REFERENCES cuentas_usuario (id),
    CONSTRAINT ck_auditoria_cuenta_accion CHECK (accion BETWEEN 1 AND 7),
    INDEX ix_auditoria_cuenta_fecha (cuenta_id, ocurrido_utc_ticks, id),
    INDEX ix_auditoria_cuenta_actor (actor_usuario_id, ocurrido_utc_ticks, id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

CREATE TRIGGER IF NOT EXISTS tr_auditoria_cuenta_no_actualizar BEFORE UPDATE ON auditoria_cuentas FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'La auditoría de cuentas no se modifica';
CREATE TRIGGER IF NOT EXISTS tr_auditoria_cuenta_no_borrar BEFORE DELETE ON auditoria_cuentas FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'La auditoría de cuentas no se elimina';
