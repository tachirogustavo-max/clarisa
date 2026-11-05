-- Estados explícitos del resultado (pendiente, numérico, cualitativo, combinado, exonerado), plan por año/grado y escala de letras.
-- Cada sentencia es repetible: tras un corte, el migrador vuelve a ejecutarlas todas antes de registrar la versión.
CREATE TABLE IF NOT EXISTS escala_cualitativa (
    id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    anio_lectivo_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    letra VARCHAR(4) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    descripcion VARCHAR(100) NULL,
    version BIGINT NOT NULL,
    CONSTRAINT uq_escala_letra UNIQUE (anio_lectivo_id, letra),
    CONSTRAINT fk_escala_anio FOREIGN KEY (anio_lectivo_id) REFERENCES anios_lectivos (id),
    CONSTRAINT ck_escala_letra CHECK (letra REGEXP '^[A-Z]{1,4}$'),
    CONSTRAINT ck_escala_descripcion CHECK (descripcion IS NULL OR CHAR_LENGTH(TRIM(descripcion)) > 0),
    CONSTRAINT ck_escala_version CHECK (version >= 1)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

CREATE TABLE IF NOT EXISTS plan_materias (
    id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    anio_lectivo_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    grado VARCHAR(40) NOT NULL,
    grado_clave VARCHAR(40) COLLATE utf8mb4_bin NOT NULL,
    materia_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    modalidad TINYINT NOT NULL,
    version BIGINT NOT NULL,
    CONSTRAINT uq_plan_grado_materia UNIQUE (anio_lectivo_id, grado_clave, materia_id),
    CONSTRAINT fk_plan_anio FOREIGN KEY (anio_lectivo_id) REFERENCES anios_lectivos (id),
    CONSTRAINT fk_plan_materia FOREIGN KEY (materia_id) REFERENCES materias (id),
    CONSTRAINT ck_plan_modalidad CHECK (modalidad BETWEEN 1 AND 4),
    CONSTRAINT ck_plan_grado CHECK (CHAR_LENGTH(TRIM(grado)) > 0),
    CONSTRAINT ck_plan_version CHECK (version >= 1)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

ALTER TABLE calificaciones
    ADD COLUMN IF NOT EXISTS tipo TINYINT NOT NULL DEFAULT 0 AFTER valor,
    ADD COLUMN IF NOT EXISTS letra VARCHAR(4) CHARACTER SET ascii COLLATE ascii_bin NULL AFTER tipo,
    ADD COLUMN IF NOT EXISTS motivo_exoneracion VARCHAR(200) NULL AFTER letra;

UPDATE calificaciones SET tipo = 1 WHERE valor IS NOT NULL AND tipo = 0;

ALTER TABLE calificaciones ALTER COLUMN tipo DROP DEFAULT;

ALTER TABLE calificaciones ADD CONSTRAINT IF NOT EXISTS ck_calificacion_resultado CHECK ((tipo = 0 AND valor IS NULL AND letra IS NULL AND motivo_exoneracion IS NULL) OR (tipo = 1 AND valor IS NOT NULL AND letra IS NULL AND motivo_exoneracion IS NULL) OR (tipo = 2 AND valor IS NULL AND letra IS NOT NULL AND motivo_exoneracion IS NULL) OR (tipo = 3 AND valor IS NOT NULL AND letra IS NOT NULL AND motivo_exoneracion IS NULL) OR (tipo = 4 AND valor IS NULL AND letra IS NULL AND motivo_exoneracion IS NOT NULL AND CHAR_LENGTH(TRIM(motivo_exoneracion)) > 0));

ALTER TABLE calificaciones ADD CONSTRAINT fk_calificacion_letra FOREIGN KEY IF NOT EXISTS (anio_lectivo_id, letra) REFERENCES escala_cualitativa (anio_lectivo_id, letra);

-- El historial previo conserva nulos en las columnas nuevas: sus filas están protegidas por disparadores y no se reescriben.
ALTER TABLE cambios_calificacion
    ADD COLUMN IF NOT EXISTS tipo_anterior TINYINT NULL AFTER nuevo,
    ADD COLUMN IF NOT EXISTS tipo_nuevo TINYINT NULL AFTER tipo_anterior,
    ADD COLUMN IF NOT EXISTS letra_anterior VARCHAR(4) CHARACTER SET ascii COLLATE ascii_bin NULL AFTER tipo_nuevo,
    ADD COLUMN IF NOT EXISTS letra_nueva VARCHAR(4) CHARACTER SET ascii COLLATE ascii_bin NULL AFTER letra_anterior,
    ADD COLUMN IF NOT EXISTS motivo VARCHAR(200) NULL AFTER letra_nueva;

ALTER TABLE cambios_calificacion ADD CONSTRAINT IF NOT EXISTS ck_cambio_resultado CHECK ((tipo_anterior IS NULL AND tipo_nuevo IS NULL AND letra_anterior IS NULL AND letra_nueva IS NULL AND motivo IS NULL) OR (tipo_anterior BETWEEN 0 AND 4 AND tipo_nuevo BETWEEN 0 AND 4 AND (nuevo IS NOT NULL) = (tipo_nuevo IN (1, 3)) AND (letra_nueva IS NOT NULL) = (tipo_nuevo IN (2, 3)) AND (anterior IS NOT NULL) = (tipo_anterior IN (1, 3)) AND (letra_anterior IS NOT NULL) = (tipo_anterior IN (2, 3)) AND (motivo IS NOT NULL) = (tipo_nuevo = 4 OR tipo_anterior = 4)));

ALTER TABLE auditoria DROP CONSTRAINT IF EXISTS ck_auditoria_accion;

ALTER TABLE auditoria ADD CONSTRAINT IF NOT EXISTS ck_auditoria_accion CHECK (accion BETWEEN 1 AND 13);
