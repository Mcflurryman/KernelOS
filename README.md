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

`Workspace` es la raíz controlada del repositorio. Los aliases configurados `Desktop` y `Documents` se habilitan únicamente si el sistema los resuelve a rutas absolutas válidas; si no están disponibles, se rechazan. También se aceptan rutas absolutas dentro de `Filesystem:AllowedRoots`. Se rechazan rutas relativas sin alias, escapes con `..` y prefijos de ruta similares. Para una ruta autorizada inexistente, `exists` devuelve HTTP 200 con `exists: false`.

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

## Document Readers Core

`POST /documents/read` implementa lectura de TXT, Markdown, JSON y CSV mediante rutas autorizadas. El body es `{"path":"Workspace/testdata/documents/text/sample.txt","format":null}`. Devuelve 200 para éxito o resultado parcial, 400 para formato o documento inválido, 403 para rutas no autorizadas, 404 para inexistentes y 413 para límites de archivo. Los límites de `DocumentReaders` son configurables; el contenido se trata como dato no confiable. PDF, DOCX, XLSX y OCR no están implementados.

Los límites se identifican estructuralmente, no por el texto de mensajes; los fallos internos devuelven 500 seguro. CSV con comillas sin cerrar devuelve 400 en modo estricto o 200 parcial con un warning si están permitidos resultados parciales.

```powershell
Invoke-RestMethod -Uri "http://localhost:PUERTO/documents/read" -Method POST -ContentType "application/json; charset=utf-8" -Body '{"path":"Workspace/testdata/documents/text/sample.txt","format":null}'
```

## Knowledge Core

Knowledge Core transforma internamente `RawDocument` en items estructurados de texto, títulos, listas, código, JSON, tablas y metadatos seguros. No expone endpoint ni herramienta todavía.

## Memory Core

Memory Core In-Memory almacena snapshots versionados de Knowledge internamente y ofrece consultas exactas deterministas. No expone endpoint ni herramienta. Persistencia, SQLite, embeddings, búsqueda semántica y RAG no están implementados.
