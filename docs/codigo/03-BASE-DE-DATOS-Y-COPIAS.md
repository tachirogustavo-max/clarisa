# 3. Base de datos y copias

## Migraciones

La base se crea y se actualiza con los scripts de `Infraestructura/Persistence/Esquema/Migraciones/`, numerados en orden (`0001_catalogos.sql`, …, `0009_grados.sql`). Van incrustados en el programa y el servidor los aplica al arrancar con `--migrar`, que es lo que hace el instalador al actualizar.

Reglas:

- **Nunca se modifica una migración ya entregada.** Su huella queda registrada en la tabla `esquema_versiones` y el servidor se niega a arrancar si no coincide. Cualquier cambio va en una migración nueva con el número siguiente.
- `EsquemaEsperado.cs` describe la forma exacta que debe tener la base. Cuando una migración cambia una tabla, se agrega su forma nueva con la versión y se marca la anterior con `Reemplazada`.
- `CatalogoRespaldo.cs` lista las tablas y columnas que entran en las copias. Si agregas una columna, agrégala también allí; la copia compara este catálogo con la base real y se niega a continuar si no coinciden.

Las tablas de historial y auditoría tienen disparadores que impiden modificarlas o borrarlas.

## Copias de seguridad

- Las crea el servidor (`Aplicacion/Features/Respaldos/`). Cada copia es un archivo `.liceo-respaldo` cifrado y comprimido, con un manifiesto de tablas y huellas. Se verifica entera después de escribirla.
- **Copias automáticas:** `CopiasAutomaticas.cs` pide una cada día a partir de la hora configurada (13:00 por defecto) y conserva las 30 más recientes. Se configura en `servidor.json`, sección `Respaldos`: `CopiasAutomaticas`, `HoraCopiaAutomatica` y `CopiasAutomaticasConservadas`.
- La clave que cifra las copias se guarda protegida en el servidor; el instalador exporta un archivo de recuperación para guardarlo fuera.

## Recuperación

`Clarisa.Mantenimiento` tiene los comandos `verificar`, `preparar`, `activar` y `revertir`. La copia siempre se carga en una **base nueva**, y la base anterior no se borra. La ventana **Recuperar una copia** del instalador (`Instalacion/Recuperacion/MotorRecuperacion.cs`) ejecuta esos comandos en orden, sin consola.

## Identificadores que conservan el nombre antiguo

El sistema se llamó internamente «Escolar» en sus primeras versiones. Estos valores siguen así a propósito, porque están escritos en bases, copias y archivos ya entregados:

- La marca de la tabla de versiones (`escolar-liceo/esquema-versionado`).
- El nombre del bloqueo del migrador (`escolar_mig:`).
- La entropía de la clave de copias (`Escolar.Respaldos.Clave.v1:`).
- La marca de formato de las copias (`Escolar.Liceo`).
- El encabezado del archivo de recuperación de la clave (`ESCOLAR-RECUPERACION-1`).

Cambiarlos dejaría ilegibles los datos existentes.
