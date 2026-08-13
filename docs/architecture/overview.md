# Arquitectura actual

> Actualización UI Foundation: `KernelOS.Web` es Blazor WebAssembly alojado por `KernelOS.Api` bajo `/ui`. El flujo es Browser → `/ui` → WebAssembly → `HttpClient` same-origin → Conversation/Health API → SQLite/Kai. No existe un segundo backend, CORS ni acceso directo de Web a Infrastructure; el fallback SPA está limitado a `/ui`. Véase [UI Foundation](ui-foundation.md).

Conversation Memory es un dominio SQLite separado de Knowledge Memory. La API `/conversations` orquesta turnos durables con serialización por conversación; no indexa conversaciones ni altera Approval, Pending o Audit Trail.

> Semantic Index Maintenance Foundation mantiene el índice derivado tras cada commit de Memory: `SQLite Memory → committed mutation → generation → bounded Channel → worker → ApplyFamilyPatchAsync`. El rebuild explícito sigue siendo la recuperación tras restart o estado `Dirty`.

> Hybrid Search Graceful Degradation permite conservar retrieval lexical-only o semantic-only cuando la otra rama falla técnicamente; no selecciona providers ni altera el rebuild semántico.

El Audit Trail interno correlaciona las transiciones de Kai, Planner, preflight, confirmation, executor y gateway read-only. Es observacional, fail-open, privado y no expone HTTP.

Kai Planner Orchestration v1 conecta Kai con Planner y executor mediante contratos de alto nivel; las Tools y approvals permanecen fuera de Kai. `Program.cs` compone módulos HTTP y `AddInfrastructure` compone los servicios internos por dominio.

KernelOS es un monolito modular .NET 8. `KernelOS.Core` contiene contratos; `KernelOS.Infrastructure` implementa proveedores y almacenamiento local; `KernelOS.Tools` delimita acciones; `KernelOS.Api` expone HTTP; `KernelOS.Tests` prueba contratos y límites.

```text
Program.cs (composition root)
 └─ Endpoint mappings por dominio
     ├─ Chat → IChatModel → OllamaChatModel
     ├─ Kai → IKaiAgent → Planner / RAG / Chat según contrato
     ├─ Planner → IPlanner/IPlanBuilder → Plan (sin efectos laterales)
     │              └─ IPlanExecutor → IExecutionPreflight → IExecutionGate → IToolRouter → Tools
     └─ Tools / Filesystem / Documents → IReadOnlyToolExecutionGateway → IToolRouter
                                                          ├─ FilesystemTool → Filesystem Capability
                                                          └─ DocumentTool → Document Readers

Filesystem autorizado → Document Readers → RawDocument → Knowledge Core
                                                     → Memory Core → Lexical Search

Embeddings Core → Ollama Embeddings Provider → Vector Index Core → Semantic Search
Memory Core → committed mutation → Semantic Index Maintenance → Vector Index Core
Memory Core → Lexical Search ─────────────────────────────────────────────────┐
Embedding query → IEmbeddingGenerator → Semantic Search ──────────────────────┼→ Hybrid Search → Context Builder → RAG Pipeline → IChatModel
                                                                                └→ degradación controlada ante fallo técnico de una rama

Conversation History → Conversation Context ───────────────→ future Kai Agent
CurrentUserMessage ────────────────────────────────────────→ future Kai Agent
                                                        ┌────→ RAG Pipeline
future Kai Agent ───────────────────────────────────────┼────→ Chat / Planner / Tools
                                                        └────→ conversation policy
```

Chat, Tool System, Planner determinista con construcción, autorización y ejecución separadas, Filesystem Read Only, Document Readers para TXT/Markdown/JSON/CSV, Knowledge Core, Memory durable local en SQLite, retrieval híbrido resiliente, Context Builder, RAG Pipeline, Conversation Context y Kai Agent Core v1 están implementados. Core conserva los contratos de Memory; Infrastructure implementa `SqliteMemoryStore` y API solo compone el módulo. Kai puede orquestar Planner mediante contratos de alto nivel, sin acceder a Tools ni `IToolRouter`; la policy exige confirmación para efectos laterales y falla cerrada ante metadata desconocida. Knowledge, retrieval, Context Builder y RAG son internos: no tienen endpoint ni Tool pública.

La Execution Approval Surface conserva un pending snapshot opaco del plan completo, permite la confirmación explícita externa y ejecuta después mediante el executor. El preflight agrega todas las decisiones antes de cualquier Tool y no conecta Kai al Planner.

Filesystem no accede a rutas no autorizadas. Document Readers reciben referencias autorizadas y el contenido documental es no confiable. Ollama es local en la configuración actual; chat y embeddings usan clientes y modelos separados.

Vector Index y embeddings siguen en memoria y derivados; el mantenimiento incremental es eventual y, tras restart o `Dirty`, requiere rebuild explícito. Approvals, pending executions y Audit Trail continúan volátiles, y Conversation Context solo vive por request. Las siguientes capas no existen todavía y no deben inferirse del diagrama: Scheduler, Windows Automation, MCP, integraciones de correo/calendario, OCR, Vision, voz y UI. El orden de evolución está en el [Architecture Roadmap](../roadmap/architecture-roadmap.md).
