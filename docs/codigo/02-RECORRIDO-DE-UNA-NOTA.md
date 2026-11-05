# 2. Recorrido de una nota

Este es el camino completo de una nota, desde la planilla del docente hasta el boletín impreso. Sirve como mapa para entender cualquier otra operación, porque todas siguen el mismo patrón.

## 1. La planilla (estación)

- `Desktop/Views/VistaPlanilla.xaml` muestra la tabla con las columnas **Nota del momento** e **Inasistencias**.
- Cada fila es un `FilaNota` (`Desktop/Presentation/Models/FilaNota.cs`). Valida lo que escribe el docente (rango de la nota, decimales, letra, inasistencias de 0 a 999) y sabe si la fila cambió.
- Al pulsar **Guardar cambios**, `ModeloPlanilla.GuardarAsync` junta solo las filas cambiadas y las envía con `ClientePlanillas` (`Desktop/Services/ClientePlanillas.cs`) a `POST api/calificaciones`.

Cada fila lleva la **versión** que el docente estaba viendo. Si otra persona guardó antes, el servidor responde que la versión es vieja y la planilla ofrece comparar en lugar de sobrescribir.

## 2. La ruta HTTP (servidor)

`Presentacion/Endpoints/EndpointsNotasCalendario.cs` recibe la petición, obtiene quién la hace (`contexto.ActorActual()`, en `Presentacion/Http/ActorHttp.cs`) y llama a `GestionCalificaciones.GuardarAsync`. No contiene reglas.

## 3. El caso de uso

`Aplicacion/Features/Calificaciones/GestionCalificaciones.cs` hace, en este orden y dentro de una sola transacción:

1. Comprueba que quien guarda sea un docente asignado a esa materia y sección, o Dirección (`AccesoCalificaciones`).
2. Comprueba que la **carga de notas** de ese momento esté abierta, con la hora del servidor.
3. Comprueba que la sección no tenga los resultados cerrados.
4. Valida decimales y tipo de evaluación según la política del año y el plan de la materia (`LoteCalificaciones`).
5. Compara las versiones para no pisar cambios de otra persona.
6. Guarda cada nota con `RegistroResultados`, que además anota el cambio en el historial (`cambios_calificacion`) y en la auditoría.

Si cualquier paso falla, no se guarda ninguna nota del lote.

## 4. La base de datos

`Infraestructura/Persistence/Repositorios/RepositorioCalificacionesMariaDb.cs` escribe en la tabla `calificaciones`: una fila por estudiante, materia y momento, con la nota, la letra, las inasistencias y una versión que sube en cada cambio.

## 5. El boletín

Cuando Dirección consulta un boletín:

- `Aplicacion/Features/Boletines/GestionBoletines.cs` reúne las notas de todas las materias del estudiante. Si la sección tiene un cierre de resultados, usa la copia fija de ese cierre.
- La nota final de cada materia (definitiva) la calcula `Dominio/Entities/CalculoDefinitiva.cs` con la política del año.
- La fila **PROMEDIO** la calcula `Dominio/Entities/CalculoPromedio.cs`: media de las notas numéricas del momento, sin materias exoneradas, no cursadas ni con letra. `PromediosBoletin.cs` arma esa fila y suma las inasistencias.
- `Infraestructura/Documentos/` dibuja el PDF (`PlantillaBoletin`, `TablaCalificacionesBoletin`, `CierreBoletin`).

En la estación, la pantalla de Boletines muestra el mismo PDF que genera el servidor. Para dibujarlo usa el lector de PDF de Windows en un proceso aparte (`Desktop/Presentation/Documents/PaginasPdf.cs` y `DibujoPdf.cs`), porque ese lector no puede usarse dentro del proceso con ventanas sin que este falle al cerrarse.

## El mismo patrón en todo el sistema

Pantalla → modelo de pantalla → cliente HTTP → ruta → caso de uso → repositorio. Si buscas dónde está algo, empieza por la carpeta del tema en `Aplicacion/Features/`.
