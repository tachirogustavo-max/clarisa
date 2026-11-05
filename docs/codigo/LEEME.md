# Documentación del código

Guía para el programador que mantenga el Sistema de Control Académico La Clarisa. Supone que conoces C# y algo de bases de datos; lo demás se explica aquí.

| Tema | Documento |
| --- | --- |
| Cómo está organizada la solución y cómo compilarla | [1. Estructura](01-ESTRUCTURA.md) |
| Qué pasa desde que un docente escribe una nota hasta que sale en el boletín | [2. Recorrido de una nota](02-RECORRIDO-DE-UNA-NOTA.md) |
| Tablas, migraciones y copias de seguridad | [3. Base de datos y copias](03-BASE-DE-DATOS-Y-COPIAS.md) |
| Dónde tocar para los cambios más comunes | [4. Cambios frecuentes](04-CAMBIOS-FRECUENTES.md) |
| Cómo trabaja el instalador | [5. Instalador](05-INSTALADOR.md) |

## En pocas palabras

- El **servidor** es una API ASP.NET Core que corre como servicio de Windows y guarda los datos en MariaDB.
- Las **estaciones** usan una aplicación WPF que habla con el servidor por HTTPS dentro de la red del liceo. Las estaciones nunca se conectan a la base de datos.
- Todo funciona **sin Internet**.
- El código y los textos están en español. Los nombres internos empiezan por `Clarisa.`.
