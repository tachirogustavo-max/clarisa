-- Política académica por año, versionada y sin valores sembrados, y cierre de resultados con copia fija por sección.
-- Cada sentencia es repetible: tras un corte, el migrador vuelve a ejecutarlas todas antes de registrar la versión.
-- Las comparaciones con columnas que admiten nulo usan <=> o IS NULL: un CHECK con resultado desconocido se daría por cumplido.
CREATE TABLE IF NOT EXISTS politicas_academicas (
    id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    anio_lectivo_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    numero INT NOT NULL,
    estado TINYINT NOT NULL,
    decimales_captura TINYINT NULL,
    decimales_resultado TINYINT NULL,
    nota_aprobatoria DECIMAL(4,2) NULL,
    formula TINYINT NULL,
    redondeo TINYINT NULL,
    tratamiento_exonerado TINYINT NULL,
    peso_lapso1 DECIMAL(5,2) NULL,
    peso_lapso2 DECIMAL(5,2) NULL,
    peso_lapso3 DECIMAL(5,2) NULL,
    motivo VARCHAR(200) NOT NULL,
    creada_por CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    creada_utc_ticks BIGINT NOT NULL,
    activada_por CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NULL,
    activada_utc_ticks BIGINT NULL,
    motivo_activacion VARCHAR(200) NULL,
    version BIGINT NOT NULL,
    estado_unico TINYINT AS (IF(estado IN (1, 2), estado, NULL)) PERSISTENT,
    CONSTRAINT uq_politica_numero UNIQUE (anio_lectivo_id, numero),
    CONSTRAINT uq_politica_estado UNIQUE (anio_lectivo_id, estado_unico),
    CONSTRAINT uq_politica_id_anio UNIQUE (id, anio_lectivo_id),
    CONSTRAINT fk_politica_anio FOREIGN KEY (anio_lectivo_id) REFERENCES anios_lectivos (id),
    CONSTRAINT ck_politica_estado CHECK (estado BETWEEN 1 AND 3),
    CONSTRAINT ck_politica_numero CHECK (numero >= 1),
    CONSTRAINT ck_politica_decimales CHECK ((decimales_captura IS NULL OR decimales_captura BETWEEN 0 AND 2) AND (decimales_resultado IS NULL OR decimales_resultado BETWEEN 0 AND 2)),
    CONSTRAINT ck_politica_aprobatoria CHECK (nota_aprobatoria IS NULL OR nota_aprobatoria BETWEEN 1 AND 20),
    CONSTRAINT ck_politica_opciones CHECK ((formula IS NULL OR formula IN (1, 2)) AND (redondeo IS NULL OR redondeo IN (1, 2, 3)) AND (tratamiento_exonerado IS NULL OR tratamiento_exonerado IN (1, 2))),
    CONSTRAINT ck_politica_pesos CHECK (((formula <=> 2) OR (peso_lapso1 IS NULL AND peso_lapso2 IS NULL AND peso_lapso3 IS NULL)) AND (peso_lapso1 IS NULL OR peso_lapso1 > 0) AND (peso_lapso2 IS NULL OR peso_lapso2 > 0) AND (peso_lapso3 IS NULL OR peso_lapso3 > 0) AND (peso_lapso1 IS NULL OR peso_lapso2 IS NULL OR peso_lapso3 IS NULL OR peso_lapso1 + peso_lapso2 + peso_lapso3 = 100)),
    CONSTRAINT ck_politica_completa CHECK (estado = 1 OR (decimales_captura IS NOT NULL AND decimales_resultado IS NOT NULL AND formula IS NOT NULL AND redondeo IS NOT NULL AND tratamiento_exonerado IS NOT NULL AND ((formula <=> 1) OR (peso_lapso1 IS NOT NULL AND peso_lapso2 IS NOT NULL AND peso_lapso3 IS NOT NULL)))),
    CONSTRAINT ck_politica_activacion CHECK ((estado = 1) = (activada_por IS NULL) AND (activada_por IS NULL) = (activada_utc_ticks IS NULL) AND (activada_por IS NULL) = (motivo_activacion IS NULL)),
    CONSTRAINT ck_politica_motivo CHECK (CHAR_LENGTH(TRIM(motivo)) > 0 AND (motivo_activacion IS NULL OR CHAR_LENGTH(TRIM(motivo_activacion)) > 0)),
    CONSTRAINT ck_politica_version CHECK (version >= 1)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

CREATE TABLE IF NOT EXISTS cierres_resultados (
    id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    anio_lectivo_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    seccion_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    numero INT NOT NULL,
    politica_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    estado TINYINT NOT NULL,
    cerrado_por CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    cerrado_utc_ticks BIGINT NOT NULL,
    anulado_por CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NULL,
    anulado_utc_ticks BIGINT NULL,
    motivo_anulacion VARCHAR(200) NULL,
    version BIGINT NOT NULL,
    vigente_marca TINYINT AS (IF(estado = 1, 1, NULL)) PERSISTENT,
    CONSTRAINT uq_cierre_numero UNIQUE (seccion_id, numero),
    CONSTRAINT uq_cierre_vigente UNIQUE (seccion_id, vigente_marca),
    CONSTRAINT fk_cierre_seccion_anio FOREIGN KEY (seccion_id, anio_lectivo_id) REFERENCES secciones (id, anio_lectivo_id),
    CONSTRAINT fk_cierre_politica_anio FOREIGN KEY (politica_id, anio_lectivo_id) REFERENCES politicas_academicas (id, anio_lectivo_id),
    CONSTRAINT ck_cierre_estado CHECK (estado IN (1, 2)),
    CONSTRAINT ck_cierre_numero CHECK (numero >= 1),
    CONSTRAINT ck_cierre_anulacion CHECK ((estado = 2) = (anulado_por IS NOT NULL) AND (anulado_por IS NULL) = (anulado_utc_ticks IS NULL) AND (anulado_por IS NULL) = (motivo_anulacion IS NULL) AND (motivo_anulacion IS NULL OR CHAR_LENGTH(TRIM(motivo_anulacion)) > 0)),
    CONSTRAINT ck_cierre_version CHECK (version >= 1)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

CREATE TABLE IF NOT EXISTS resultados_cerrados (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    cierre_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    inscripcion_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    materia_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    modalidad TINYINT NULL,
    tipo_lapso1 TINYINT NOT NULL,
    valor_lapso1 DECIMAL(4,2) NULL,
    letra_lapso1 VARCHAR(4) CHARACTER SET ascii COLLATE ascii_bin NULL,
    version_lapso1 BIGINT NOT NULL,
    tipo_lapso2 TINYINT NOT NULL,
    valor_lapso2 DECIMAL(4,2) NULL,
    letra_lapso2 VARCHAR(4) CHARACTER SET ascii COLLATE ascii_bin NULL,
    version_lapso2 BIGINT NOT NULL,
    tipo_lapso3 TINYINT NOT NULL,
    valor_lapso3 DECIMAL(4,2) NULL,
    letra_lapso3 VARCHAR(4) CHARACTER SET ascii COLLATE ascii_bin NULL,
    version_lapso3 BIGINT NOT NULL,
    estado_definitiva TINYINT NOT NULL,
    valor_definitiva DECIMAL(4,2) NULL,
    CONSTRAINT uq_resultado_cerrado UNIQUE (cierre_id, inscripcion_id, materia_id),
    CONSTRAINT fk_resultado_cierre FOREIGN KEY (cierre_id) REFERENCES cierres_resultados (id),
    CONSTRAINT fk_resultado_inscripcion FOREIGN KEY (inscripcion_id) REFERENCES inscripciones (id),
    CONSTRAINT fk_resultado_materia FOREIGN KEY (materia_id) REFERENCES materias (id),
    CONSTRAINT ck_resultado_modalidad CHECK (modalidad IS NULL OR modalidad BETWEEN 1 AND 4),
    CONSTRAINT ck_resultado_lapso1 CHECK (tipo_lapso1 BETWEEN 0 AND 4 AND (valor_lapso1 IS NOT NULL) = (tipo_lapso1 IN (1, 3)) AND (letra_lapso1 IS NOT NULL) = (tipo_lapso1 IN (2, 3)) AND (valor_lapso1 IS NULL OR valor_lapso1 BETWEEN 1 AND 20) AND version_lapso1 >= 0 AND (tipo_lapso1 = 0 OR version_lapso1 >= 1)),
    CONSTRAINT ck_resultado_lapso2 CHECK (tipo_lapso2 BETWEEN 0 AND 4 AND (valor_lapso2 IS NOT NULL) = (tipo_lapso2 IN (1, 3)) AND (letra_lapso2 IS NOT NULL) = (tipo_lapso2 IN (2, 3)) AND (valor_lapso2 IS NULL OR valor_lapso2 BETWEEN 1 AND 20) AND version_lapso2 >= 0 AND (tipo_lapso2 = 0 OR version_lapso2 >= 1)),
    CONSTRAINT ck_resultado_lapso3 CHECK (tipo_lapso3 BETWEEN 0 AND 4 AND (valor_lapso3 IS NOT NULL) = (tipo_lapso3 IN (1, 3)) AND (letra_lapso3 IS NOT NULL) = (tipo_lapso3 IN (2, 3)) AND (valor_lapso3 IS NULL OR valor_lapso3 BETWEEN 1 AND 20) AND version_lapso3 >= 0 AND (tipo_lapso3 = 0 OR version_lapso3 >= 1)),
    CONSTRAINT ck_resultado_definitiva CHECK (estado_definitiva BETWEEN 0 AND 4 AND (valor_definitiva IS NOT NULL) = (estado_definitiva = 1) AND (valor_definitiva IS NULL OR valor_definitiva BETWEEN 1 AND 20)),
    INDEX ix_resultado_inscripcion (inscripcion_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

CREATE TRIGGER IF NOT EXISTS tr_resultado_cerrado_no_actualizar BEFORE UPDATE ON resultados_cerrados FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Los resultados cerrados no se modifican';

CREATE TRIGGER IF NOT EXISTS tr_resultado_cerrado_no_borrar BEFORE DELETE ON resultados_cerrados FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Los resultados cerrados no se eliminan';

ALTER TABLE auditoria DROP CONSTRAINT IF EXISTS ck_auditoria_accion;

ALTER TABLE auditoria ADD CONSTRAINT IF NOT EXISTS ck_auditoria_accion CHECK (accion BETWEEN 1 AND 18);
