# Sistema de Control Académico La Clarisa

Sistema de control de estudios del Liceo Bolivariano Clarisa Este de Trejo, en San Fernando de Apure.

Con él, Dirección prepara el año escolar, registra estudiantes y docentes, arma las secciones y decide cuándo se cargan notas. Los docentes cargan las notas y las inasistencias de sus materias, y al cerrar cada momento salen los boletines en el formato del liceo. El servidor guarda una copia de seguridad todos los días.

Trabaja en la red local del liceo y no necesita Internet. Una computadora hace de servidor y las demás se conectan a ella.

## Documentación

El [manual de uso](docs/manuales/Manual-de-uso-La-Clarisa.pdf) es para el personal del liceo. La [documentación del código](docs/codigo/LEEME.md) es para quien mantenga el sistema.

## Compilar

Hace falta el SDK de .NET 10 con soporte para escritorio:

```
dotnet build Clarisa.sln
```

El servidor está hecho con ASP.NET Core, las estaciones con WPF y la base de datos es MariaDB 11.4, que ya viene dentro del instalador. Los PDF se generan con PDFsharp/MigraDoc.

## Licencias

La carpeta `licencias` tiene las licencias de las bibliotecas que usa el sistema. El instalador las copia junto con los programas.
