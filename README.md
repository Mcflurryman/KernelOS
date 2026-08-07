# KernelOS

KernelOS es una plataforma personal de IA local. Kai es el asistente previsto sobre esa plataforma. El proyecto es un monolito modular .NET 8: Core define contratos; Infrastructure integra proveedores locales; Tools controla acciones; Api expone HTTP.

## Estado actual

Están implementados chat local mediante Ollama, Tool System, Planner determinista inicial, Filesystem Capability Read Only, Document Readers (TXT, Markdown, JSON y CSV), Knowledge Core, Memory Core In-Memory, retrieval interno, Context Builder y RAG Pipeline internos.

Siguen pendientes Kai Agent, historial conversacional y experiencia pública de preguntas sobre documentos. Tampoco existen Scheduler, automatización de Windows, MCP, integraciones de correo/calendario, OCR, voz o UI.

## Requisitos y ejecución

Se necesita .NET 8. Para chat o embeddings se necesita Ollama local y los modelos configurados; KernelOS nunca descarga modelos automáticamente.

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/KernelOS.Api
```

La validación reproducible es:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\validate.ps1
```

## Capacidades y límites

- Filesystem solo permite `search`, `exists`, `metadata`, `resolve` y `list` dentro de raíces autorizadas. `Workspace` siempre está disponible; `Desktop` y `Documents` son opcionales según el sistema.
- Los documentos se tratan como datos no confiables. No se ejecutan instrucciones, macros ni contenido activo.
- El contenido y los modelos se procesan localmente por la configuración actual. Cualquier proveedor remoto futuro requerirá una decisión explícita.
- No hay operaciones de escritura, endpoints de Knowledge/Search/Embeddings ni Tools públicas para esos componentes internos.

## Documentación

- [Visión del proyecto](PROJECT.md)
- [Arquitectura actual](docs/architecture/overview.md)
- [Roadmap](docs/roadmap/roadmap.md)
- [Decisiones arquitectónicas](docs/decisions/)
- [Guía de desarrollo](docs/guides/git-workflow.md)
- [Changelog](CHANGELOG.md)
