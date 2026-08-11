# Arquitectura actual

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
Memory Core → Lexical Search ────────────────────────────────────────────────┐
Embedding query → IEmbeddingGenerator → Semantic Search ──────────────────────┼→ Hybrid Search
                                                                               ┘
Hybrid Search references → Context Builder → ContextPack → RAG Pipeline → IChatModel

Conversation History → Conversation Context ───────────────→ future Kai Agent
CurrentUserMessage ────────────────────────────────────────→ future Kai Agent
                                                        ┌────→ RAG Pipeline
future Kai Agent ───────────────────────────────────────┼────→ Chat / Planner / Tools
                                                        └────→ conversation policy
```

Chat, Tool System, Planner determinista con construcción, autorización y ejecución separadas, Filesystem Read Only, Document Readers para TXT/Markdown/JSON/CSV, Knowledge Core, Memory Core In-Memory, retrieval, Context Builder, RAG Pipeline, Conversation Context y Kai Agent Core v1 están implementados. Kai puede orquestar Planner mediante contratos de alto nivel, sin acceder a Tools ni `IToolRouter`; la policy exige confirmación para efectos laterales y falla cerrada ante metadata desconocida. Knowledge, retrieval, Context Builder y RAG son internos: no tienen endpoint ni Tool pública.

La Execution Approval Surface conserva un pending snapshot opaco del plan completo, permite la confirmación explícita externa y ejecuta después mediante el executor. El preflight agrega todas las decisiones antes de cualquier Tool y no conecta Kai al Planner.

Filesystem no accede a rutas no autorizadas. Document Readers reciben referencias autorizadas y el contenido documental es no confiable. Ollama es local en la configuración actual; chat y embeddings usan clientes y modelos separados.

Las siguientes capas no existen todavía y no deben inferirse del diagrama: Scheduler, Windows Automation, MCP, integraciones de correo/calendario, OCR, Vision, voz y UI. El orden de evolución está en el [Architecture Roadmap](../roadmap/architecture-roadmap.md).
