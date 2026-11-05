# 1. Estructura de la solución

## Compilar

Necesitas el SDK de .NET 10 con las cargas de escritorio (WPF). Abre `Clarisa.sln` en Visual Studio o, desde una terminal en la carpeta del proyecto:

```powershell
dotnet build Clarisa.sln
```

La primera compilación descarga los paquetes NuGet; después no hace falta Internet.

## Proyectos

Todos los proyectos están directamente bajo la solución, cada uno en una carpeta con su nombre.

| Proyecto | Programa o biblioteca | Qué contiene |
| --- | --- | --- |
| `Dominio` | `Clarisa.Domain.dll` | Las entidades (Estudiante, Calificación, Sección…) y sus reglas básicas. No conoce bases de datos, HTTP ni pantallas. |
| `Aplicacion` | `Clarisa.Application.dll` | Los casos de uso: qué se puede hacer, quién puede hacerlo y en qué orden. Define interfaces (puertos) para guardar y leer datos. |
| `Infraestructura` | `Clarisa.Infrastructure.dll` | Implementa esas interfaces con MariaDB, genera los PDF, cifra y verifica las copias de seguridad. |
| `Presentacion` | `Clarisa.Api.exe` | El servidor: rutas HTTP, sesiones, permisos por rol y el arranque como servicio de Windows. |
| `Desktop` | `Clarisa.Desktop.exe` | La aplicación de las estaciones (WPF). |
| `Mantenimiento` | `Clarisa.Mantenimiento.exe` | Herramienta de consola del servidor: copias, claves y recuperación. |
| `Instalacion` | `Clarisa.Instalacion.dll` | El motor del instalador: cada paso comprueba, actúa y vuelve a comprobar. |
| `Setup` | `Clarisa.Setup.exe` | Las ventanas del instalador, la desinstalación y la recuperación de copias. |
| `Instalador` | `Instalar-Clarisa-<versión>.exe` | El archivo que se entrega: lleva dentro el instalador y el paquete, y los descomprime al abrirlo. |

## Quién usa a quién

```
Dominio  ←  Aplicacion  ←  Infraestructura
                ↑               ↑
                └── Presentacion ┘

Desktop  ──HTTPS──▶  Presentacion
Setup    ──▶  Instalacion
Mantenimiento ──▶ Aplicacion, Infraestructura
```

- `Dominio` no depende de nadie.
- `Aplicacion` solo conoce `Dominio`. Las reglas académicas viven aquí y en `Dominio`, nunca en las pantallas ni en el SQL.
- `Desktop` no referencia a ningún otro proyecto: todo lo pide al servidor por HTTP.

## Dentro de cada proyecto

- **Aplicacion/Features/** tiene una carpeta por tema (`Calificaciones`, `Boletines`, `Estudiantes`…). En cada una hay un archivo de contratos (los datos que entran y salen) y una o varias clases `Gestion…` con los casos de uso.
- **Infraestructura/Persistence/Repositorios/** tiene el SQL de cada tabla. **Persistence/Esquema/Migraciones/** tiene los scripts que crean y actualizan la base.
- **Presentacion/Endpoints/** declara las rutas HTTP. Cada ruta solo traduce la petición y llama al caso de uso.
- **Desktop/Views/** son las pantallas (XAML). **Desktop/Presentation/Models/** guarda el estado de cada pantalla. **Desktop/Services/** son los clientes HTTP.

## Reglas del código

- Archivos cortos. Si uno crece demasiado, se separa por responsabilidad.
- Nombres, comentarios y mensajes en español; los mensajes al usuario, tuteando.
- Un comentario explica **por qué** se hace algo, no repite lo que dice el código.
