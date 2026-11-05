# 5. Instalador

## El paquete

Cada versión se entrega como un solo archivo, `Instalar-Clarisa-<versión>.exe`, que es el proyecto `Instalador`. Lleva dentro, comprimida, la carpeta del instalador: `Clarisa.Setup.exe`, la carpeta `paquete` (los programas ya publicados, MariaDB 11.4 y las licencias) y las licencias del propio instalador. No hace falta instalar .NET en el liceo.

Al abrirlo pide permiso de administrador, descomprime esa carpeta en `C:\ProgramData\Clarisa-Instalador-…` con permisos solo para administradores y el sistema, abre `Clarisa.Setup.exe` y borra la carpeta cuando este se cierra.

La huella del paquete queda dentro de `Clarisa.Setup.exe`: si alguien cambia un archivo del paquete, el instalador se niega a usarlo.

## Cómo trabaja el instalador

`Instalacion/CatalogoPasos.cs` lista los pasos del servidor y de la estación. Cada paso (`IPasoInstalacion`) tiene dos métodos:

- `ComprobarAsync`: dice si lo que ese paso debe dejar listo ya está bien.
- `AplicarAsync`: lo hace.

El motor (`MotorInstalacion`) comprueba, aplica solo si hace falta y vuelve a comprobar. Por eso volver a ejecutar el instalador repara lo que falte sin repetir lo que ya está bien.

Al actualizar el servidor, antes de cambiar programas, el paso `preparar-actualizacion` toma una copia verificada y guarda los programas anteriores. Si algo falla antes de migrar la base, se reponen solos.

El instalador solo modifica lo que creó él mismo: lo registra en un estado protegido y no toca servicios, carpetas o reglas de firewall ajenas, aunque tengan el mismo nombre.

## Nombres que no se cambian

El servicio de Windows se llama `Clarisa.Estudios`, las carpetas de instalación son `Clarisa Estudios` y la regla de firewall `Clarisa Estudios - Servidor HTTPS`. Lo que ve el usuario dice «Sistema de Control Académico La Clarisa», pero esos nombres técnicos están en los equipos ya instalados y cambiarlos obligaría a mover datos.

Las versiones anteriores tenían los programas con el nombre `Escolar.*`. El instalador los reconoce al actualizar (`Instalacion/Ejecucion/ProgramasAnteriores.cs`) y los reemplaza por los `Clarisa.*`.
