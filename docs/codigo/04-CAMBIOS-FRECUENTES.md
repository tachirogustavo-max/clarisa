# 4. Cambios frecuentes

| Quiero… | Dónde |
| --- | --- |
| Cambiar un texto de una pantalla | El `.xaml` de `Desktop/Views/` o el modelo en `Desktop/Presentation/Models/`. |
| Cambiar un mensaje de error | En el caso de uso de `Aplicacion/Features/<tema>/` o en la entidad de `Dominio/Entities/`. |
| Cambiar quién puede hacer algo | El caso de uso (llama a `Autorizacion.ExigirAdministracion` o comprueba la asignación del docente) y la ruta en `Presentacion/Endpoints/`. |
| Cambiar una regla académica | `Dominio/Entities/` (por ejemplo `CalculoDefinitiva`, `CalculoPromedio`, `PoliticaAcademica`). |
| Agregar un dato a una tabla | Migración nueva, entidad, repositorio, `EsquemaEsperado` y `CatalogoRespaldo` (ver [Base de datos](03-BASE-DE-DATOS-Y-COPIAS.md)). |
| Cambiar el formato del boletín | `Infraestructura/Documentos/PlantillaBoletin.cs` y `TablaCalificacionesBoletin.cs`. La pantalla muestra el mismo PDF, así que no hay que tocar nada más. |
| Cambiar los nombres de las firmas | Se hace desde la propia aplicación: **Boletines → Firmas del boletín**. No requiere cambiar código. |
| Agregar o renombrar un año de estudio (6to año, por ejemplo) | Desde la aplicación: **Secciones → Años de estudio**. Al renombrar, `GestionGrados` actualiza también las secciones y el plan. |
| Cambiar la hora o la cantidad de copias automáticas | `servidor.json`, sección `Respaldos`. |
| Agregar una pantalla | Vista en `Desktop/Views/`, modelo en `Desktop/Presentation/Models/`, cliente en `Desktop/Services/` y su entrada en `Desktop/Configuration/FabricaModulos.cs`. |

## Antes de entregar un cambio

1. Compila la solución sin advertencias.
2. Si cambiaste la base de datos, aplica la migración sobre una copia de la base antes de tocar la del liceo.
3. Si cambiaste una pantalla, ábrela conectada a un servidor de prueba y revísala con los dos perfiles: Dirección y docente.

## Cuidados

- Una nota pendiente (`null`) no es un cero. Nunca se rellenan valores que faltan.
- El servidor vuelve a comprobar todo lo que valida la pantalla: no confíes en que la estación ya lo hizo.
- Las operaciones que guardan varias cosas lo hacen dentro de una unidad de trabajo (`fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, …)`); no confirmes a medias.
- No guardes contraseñas, claves ni datos de estudiantes en registros ni en mensajes de error.
