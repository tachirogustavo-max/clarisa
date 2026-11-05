CREATE TABLE IF NOT EXISTS bloqueo_escritura (
    id TINYINT NOT NULL PRIMARY KEY,
    CONSTRAINT ck_bloqueo_unico CHECK (id = 1)
) ENGINE=InnoDB;
INSERT IGNORE INTO bloqueo_escritura (id) VALUES (1);

CREATE TABLE IF NOT EXISTS anios_lectivos (
    id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    nombre VARCHAR(40) NOT NULL,
    nombre_clave VARCHAR(40) COLLATE utf8mb4_bin NOT NULL,
    inicio DATE NOT NULL,
    fin DATE NOT NULL,
    lapso_actual_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NULL,
    version BIGINT NOT NULL,
    CONSTRAINT uq_anio_nombre UNIQUE (nombre_clave),
    CONSTRAINT ck_anio_fechas CHECK (fin > inicio),
    CONSTRAINT ck_anio_version CHECK (version >= 1),
    CONSTRAINT ck_anio_nombre CHECK (CHAR_LENGTH(TRIM(nombre)) > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

CREATE TABLE IF NOT EXISTS lapsos (
    id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    anio_lectivo_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    nombre VARCHAR(60) NOT NULL,
    nombre_clave VARCHAR(60) COLLATE utf8mb4_bin NOT NULL,
    orden TINYINT NOT NULL,
    inicio DATE NOT NULL,
    fin DATE NOT NULL,
    version BIGINT NOT NULL,
    CONSTRAINT uq_lapso_orden UNIQUE (anio_lectivo_id, orden),
    CONSTRAINT uq_lapso_nombre UNIQUE (anio_lectivo_id, nombre_clave),
    CONSTRAINT uq_lapso_anio_id UNIQUE (anio_lectivo_id, id),
    CONSTRAINT uq_lapso_id_anio UNIQUE (id, anio_lectivo_id),
    CONSTRAINT fk_lapso_anio FOREIGN KEY (anio_lectivo_id) REFERENCES anios_lectivos (id),
    CONSTRAINT ck_lapso_orden CHECK (orden BETWEEN 1 AND 3),
    CONSTRAINT ck_lapso_fechas CHECK (fin >= inicio),
    CONSTRAINT ck_lapso_version CHECK (version >= 1)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

ALTER TABLE anios_lectivos ADD CONSTRAINT fk_anio_lapso_actual
FOREIGN KEY IF NOT EXISTS (id, lapso_actual_id) REFERENCES lapsos (anio_lectivo_id, id);

CREATE TABLE IF NOT EXISTS estudiantes (
    id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    identificacion VARCHAR(30) CHARACTER SET ascii COLLATE ascii_bin NULL,
    nombres VARCHAR(80) NOT NULL,
    apellidos VARCHAR(80) NOT NULL,
    fecha_nacimiento DATE NULL,
    activo TINYINT NOT NULL,
    version BIGINT NOT NULL,
    CONSTRAINT uq_estudiante_identificacion UNIQUE (identificacion),
    CONSTRAINT ck_estudiante_activo CHECK (activo IN (0, 1)),
    CONSTRAINT ck_estudiante_version CHECK (version >= 1),
    CONSTRAINT ck_estudiante_nombres CHECK (CHAR_LENGTH(TRIM(nombres)) > 0 AND CHAR_LENGTH(TRIM(apellidos)) > 0),
    INDEX ix_estudiante_nombre (apellidos, nombres, id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

CREATE TABLE IF NOT EXISTS docentes (
    id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    identificacion VARCHAR(30) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    nombres VARCHAR(80) NOT NULL,
    apellidos VARCHAR(80) NOT NULL,
    correo VARCHAR(254) NULL,
    activo TINYINT NOT NULL,
    version BIGINT NOT NULL,
    CONSTRAINT uq_docente_identificacion UNIQUE (identificacion),
    CONSTRAINT ck_docente_activo CHECK (activo IN (0, 1)),
    CONSTRAINT ck_docente_version CHECK (version >= 1),
    CONSTRAINT ck_docente_nombres CHECK (CHAR_LENGTH(TRIM(nombres)) > 0 AND CHAR_LENGTH(TRIM(apellidos)) > 0),
    INDEX ix_docente_nombre (apellidos, nombres, id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

CREATE TABLE IF NOT EXISTS materias (
    id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    codigo VARCHAR(20) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    nombre VARCHAR(100) NOT NULL,
    activa TINYINT NOT NULL,
    version BIGINT NOT NULL,
    CONSTRAINT uq_materia_codigo UNIQUE (codigo),
    CONSTRAINT ck_materia_activa CHECK (activa IN (0, 1)),
    CONSTRAINT ck_materia_version CHECK (version >= 1),
    CONSTRAINT ck_materia_nombre CHECK (CHAR_LENGTH(TRIM(nombre)) > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

CREATE TABLE IF NOT EXISTS secciones (
    id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    anio_lectivo_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    grado VARCHAR(40) NOT NULL,
    grado_clave VARCHAR(40) COLLATE utf8mb4_bin NOT NULL,
    nombre VARCHAR(20) NOT NULL,
    nombre_clave VARCHAR(20) COLLATE utf8mb4_bin NOT NULL,
    version BIGINT NOT NULL,
    CONSTRAINT uq_seccion_grado_nombre UNIQUE (anio_lectivo_id, grado_clave, nombre_clave),
    CONSTRAINT uq_seccion_id_anio UNIQUE (id, anio_lectivo_id),
    CONSTRAINT fk_seccion_anio FOREIGN KEY (anio_lectivo_id) REFERENCES anios_lectivos (id),
    CONSTRAINT ck_seccion_version CHECK (version >= 1)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

CREATE TABLE IF NOT EXISTS inscripciones (
    id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    estudiante_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    seccion_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    anio_lectivo_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    fecha DATE NOT NULL,
    estado TINYINT NOT NULL,
    fecha_retiro DATE NULL,
    version BIGINT NOT NULL,
    activa_marca TINYINT GENERATED ALWAYS AS (IF(estado = 1, 1, NULL)) PERSISTENT,
    CONSTRAINT uq_inscripcion_activa UNIQUE (estudiante_id, seccion_id, activa_marca),
    CONSTRAINT uq_inscripcion_id_anio UNIQUE (id, anio_lectivo_id),
    CONSTRAINT fk_inscripcion_estudiante FOREIGN KEY (estudiante_id) REFERENCES estudiantes (id),
    CONSTRAINT fk_inscripcion_seccion_anio FOREIGN KEY (seccion_id, anio_lectivo_id) REFERENCES secciones (id, anio_lectivo_id),
    CONSTRAINT ck_inscripcion_estado CHECK ((estado = 1 AND fecha_retiro IS NULL) OR (estado = 2 AND fecha_retiro >= fecha AND fecha_retiro IS NOT NULL)),
    CONSTRAINT ck_inscripcion_version CHECK (version >= 1),
    INDEX ix_inscripcion_seccion_estado (seccion_id, estado, estudiante_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

CREATE TABLE IF NOT EXISTS asignaciones_docentes (
    id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    seccion_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    materia_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    docente_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    version BIGINT NOT NULL,
    CONSTRAINT uq_asignacion_triple UNIQUE (seccion_id, materia_id, docente_id),
    CONSTRAINT fk_asignacion_seccion FOREIGN KEY (seccion_id) REFERENCES secciones (id),
    CONSTRAINT fk_asignacion_materia FOREIGN KEY (materia_id) REFERENCES materias (id),
    CONSTRAINT fk_asignacion_docente FOREIGN KEY (docente_id) REFERENCES docentes (id),
    CONSTRAINT ck_asignacion_version CHECK (version >= 1),
    INDEX ix_asignacion_docente (docente_id, seccion_id, materia_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;
