-- Años de estudio (1er año, 2do año...) que Dirección puede crear, renombrar, ordenar y desactivar.
-- Las secciones y el plan guardan el nombre del año; al renombrarlo se actualizan en la misma operación.
CREATE TABLE IF NOT EXISTS grados (
    id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    nombre VARCHAR(40) NOT NULL,
    nombre_clave VARCHAR(40) COLLATE utf8mb4_bin NOT NULL,
    orden SMALLINT NOT NULL,
    activo TINYINT NOT NULL,
    version BIGINT NOT NULL,
    CONSTRAINT uq_grado_nombre UNIQUE (nombre_clave),
    CONSTRAINT ck_grado_orden CHECK (orden BETWEEN 1 AND 99),
    CONSTRAINT ck_grado_activo CHECK (activo IN (0, 1)),
    CONSTRAINT ck_grado_version CHECK (version >= 1),
    CONSTRAINT ck_grado_nombre CHECK (CHAR_LENGTH(TRIM(nombre)) > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

INSERT IGNORE INTO grados (id, nombre, nombre_clave, orden, activo, version) VALUES
    ('0193f1a0-0000-7000-8000-000000000001', '1er año', '1ER AÑO', 1, 1, 1),
    ('0193f1a0-0000-7000-8000-000000000002', '2do año', '2DO AÑO', 2, 1, 1),
    ('0193f1a0-0000-7000-8000-000000000003', '3er año', '3ER AÑO', 3, 1, 1),
    ('0193f1a0-0000-7000-8000-000000000004', '4to año', '4TO AÑO', 4, 1, 1),
    ('0193f1a0-0000-7000-8000-000000000005', '5to año', '5TO AÑO', 5, 1, 1);

-- Años escritos de otra forma en bases anteriores: se conservan al final de la lista para que ninguna sección quede sin su año.
INSERT IGNORE INTO grados (id, nombre, nombre_clave, orden, activo, version)
SELECT UUID(), MIN(grado), grado_clave, 90, 1, 1 FROM secciones GROUP BY grado_clave;

INSERT IGNORE INTO grados (id, nombre, nombre_clave, orden, activo, version)
SELECT UUID(), MIN(grado), grado_clave, 90, 1, 1 FROM plan_materias GROUP BY grado_clave;
