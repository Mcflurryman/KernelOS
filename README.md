# KernelOS

KernelOS incorpora un Audit Trail interno para correlacionar decisiones y ejecuciones con metadata segura; no almacena payloads sensibles ni expone una API pública.

Kai Planner Orchestration v1 permite `POST /kai` con una Tool explícita: las acciones read-only autorizadas se ejecutan bajo policy; los side effects devuelven `RequiresConfirmation` y un pending externo. Los planes multi-task completan preflight de autorización antes de cualquier Tool. Kai no autoaprueba ni ejecuta fuera del executor.

KernelOS es una plataforma personal de IA local. Kai es el asistente previsto sobre esa plataforma. El proyecto es un monolito modular .NET 8: Core define contratos; Infrastructure integra proveedores locales; Tools controla acciones; Api expone HTTP.

## Estado actual

Están implementados chat local mediante Ollama, Tool System, Planner determinista con planificación, autorización y ejecución separadas, Filesystem Capability Read Only, Document Readers (TXT, Markdown, JSON y CSV), Knowledge Core y Memory durable local en SQLite, retrieval interno, Context Builder, RAG Pipeline, Conversation Context Core y Kai Agent Core v1. Conversation Context recibe historial reciente del caller, selecciona por presupuesto y mantiene separado el mensaje actual.

Siguen pendientes la persistencia de conversaciones entre sesiones y experiencia pública de preguntas sobre documentos. La construcción de un plan no ejecuta Tools; las acciones con efectos laterales requieren aprobaciones de un solo uso, ligadas a plan, tarea y fingerprint, creadas mediante confirmación API explícita sobre un snapshot. La ejecución es secuencial y no hace rollback de efectos externos. Approvals, pending executions y Audit Trail siguen en memoria; Conversation Context no es una memoria conversacional persistente. Tampoco existen Scheduler, automatización de Windows, MCP, integraciones de correo/calendario, OCR, voz o UI.

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
- Memory usa SQLite local; Vector Index y embeddings continúan en memoria y no se persisten con Memory. La configuración, migraciones y límites de seguridad se describen en la arquitectura de persistencia.

## Documentación

- [Visión del proyecto](PROJECT.md)
- [Arquitectura actual](docs/architecture/overview.md)
- [Persistence Foundation](docs/architecture/persistence-foundation.md)
- [Execution Audit Trail](docs/architecture/execution-audit-trail.md)
- [Roadmap](docs/roadmap/roadmap.md)
- [Decisiones arquitectónicas](docs/decisions/)
- [Guía de desarrollo](docs/guides/git-workflow.md)
- [Changelog](CHANGELOG.md)
