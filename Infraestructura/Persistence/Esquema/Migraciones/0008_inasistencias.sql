-- Inasistencias de cada estudiante por materia y momento. El total del momento que muestra el boletín es la suma de estas.
-- Una materia exonerada no se cursa, así que no lleva inasistencias.
ALTER TABLE calificaciones ADD COLUMN IF NOT EXISTS inasistencias SMALLINT NULL;

ALTER TABLE calificaciones ADD CONSTRAINT IF NOT EXISTS ck_calificacion_inasistencias CHECK (inasistencias IS NULL OR (inasistencias BETWEEN 0 AND 999 AND tipo <> 4));

ALTER TABLE cambios_calificacion ADD COLUMN IF NOT EXISTS inasistencias_anteriores SMALLINT NULL;

ALTER TABLE cambios_calificacion ADD COLUMN IF NOT EXISTS inasistencias_nuevas SMALLINT NULL;

ALTER TABLE cambios_calificacion ADD CONSTRAINT IF NOT EXISTS ck_cambio_inasistencias CHECK ((inasistencias_anteriores IS NULL OR inasistencias_anteriores BETWEEN 0 AND 999) AND (inasistencias_nuevas IS NULL OR inasistencias_nuevas BETWEEN 0 AND 999));
