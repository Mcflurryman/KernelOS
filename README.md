# KernelOS

KernelOS es una plataforma personal de IA local. Kai es el asistente que opera sobre ella.

> Estado actual: API .NET 8, chat local con Ollama, Tool System, Planner determinista inicial y Filesystem Capability Read Only completada.

## Ejecutar localmente

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/KernelOS.Api
```

La API expone `GET /`, `GET /health`, `GET /health/ollama`, `POST /chat`, endpoints de herramientas, `POST /planner/execute` y `POST /filesystem/{operation}`.

## Filesystem Read Only

Las operaciones disponibles son `search`, `exists`, `metadata`, `resolve` y `list`. `operation` se declara solo en la URL; el cuerpo contiene únicamente `arguments`. Todas las solicitudes pasan por `IToolRouter` y `FilesystemTool`.

`Workspace` es la raíz controlada del repositorio. También se soportan los aliases configurados `Desktop` y `Documents`, y rutas absolutas dentro de `Filesystem:AllowedRoots`. Se rechazan rutas relativas sin alias, escapes con `..` y prefijos de ruta similares. Para una ruta autorizada inexistente, `exists` devuelve HTTP 200 con `exists: false`.

Ejemplos PowerShell de una sola línea (sustituye `PUERTO` por el mostrado al iniciar la API):

```powershell
Invoke-RestMethod -Uri "http://localhost:PUERTO/filesystem/search" -Method POST -ContentType "application/json; charset=utf-8" -Body '{"arguments":{"path":"Workspace/testdata/filesystem","pattern":"*.cs","recursive":true,"maxResults":20}}'
Invoke-RestMethod -Uri "http://localhost:PUERTO/filesystem/exists" -Method POST -ContentType "application/json; charset=utf-8" -Body '{"arguments":{"path":"Workspace/testdata/filesystem/sample.cs"}}'
Invoke-RestMethod -Uri "http://localhost:PUERTO/filesystem/metadata" -Method POST -ContentType "application/json; charset=utf-8" -Body '{"arguments":{"path":"Workspace/testdata/filesystem/sample.cs"}}'
```

La capacidad no lee contenido de documentos ni implementa escritura, copia, movimiento, renombrado, eliminación, creación o Watch.

## Documentación

- [Proyecto](PROJECT.md)
- [Arquitectura](docs/architecture/overview.md)
- [Filesystem Capability](docs/architecture/filesystem-capability.md)
- [Roadmap](docs/roadmap/roadmap.md)

## Desarrollo

El desarrollo se realiza mediante ramas y Pull Requests; `main` representa código estable. Ejecuta la validación local reproducible con `powershell -ExecutionPolicy Bypass -File .\scripts\validate.ps1` antes de abrir un PR. El bypass solo se aplica a ese proceso; cuando esté disponible, también puede usarse `pwsh -File .\scripts\validate.ps1`. KernelOS CI valida restore, build y tests en cada Pull Request hacia `main` y en las ramas permitidas. La versión actual es `0.4.0`.

- [Flujo Git](docs/guides/git-workflow.md)
- [Versionado](docs/guides/versioning.md)
- [Changelog](CHANGELOG.md)
