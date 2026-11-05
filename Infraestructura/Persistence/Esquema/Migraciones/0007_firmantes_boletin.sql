-- Nombres impresos bajo las firmas del boletín. Cada cambio añade un registro con número consecutivo y nunca se reescribe.
-- Sin registros, el servidor usa los nombres de la sección Informes de su configuración.
CREATE TABLE IF NOT EXISTS firmantes_boletin (
    id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    numero INT NOT NULL,
    director VARCHAR(120) NULL,
    coordinador_evaluacion VARCHAR(120) NULL,
    coordinador_estudios VARCHAR(120) NULL,
    motivo VARCHAR(200) NOT NULL,
    registrado_por CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    registrado_utc_ticks BIGINT NOT NULL,
    CONSTRAINT uq_firmantes_numero UNIQUE (numero),
    CONSTRAINT ck_firmantes_numero CHECK (numero >= 1),
    CONSTRAINT ck_firmantes_nombres CHECK (director IS NOT NULL OR coordinador_evaluacion IS NOT NULL OR coordinador_estudios IS NOT NULL),
    CONSTRAINT ck_firmantes_motivo CHECK (CHAR_LENGTH(TRIM(motivo)) > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;

CREATE TRIGGER IF NOT EXISTS tr_firmantes_no_actualizar BEFORE UPDATE ON firmantes_boletin FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Los firmantes registrados no se modifican';

CREATE TRIGGER IF NOT EXISTS tr_firmantes_no_borrar BEFORE DELETE ON firmantes_boletin FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Los firmantes registrados no se eliminan';

ALTER TABLE auditoria DROP CONSTRAINT IF EXISTS ck_auditoria_accion;

ALTER TABLE auditoria ADD CONSTRAINT IF NOT EXISTS ck_auditoria_accion CHECK (accion BETWEEN 1 AND 19);
