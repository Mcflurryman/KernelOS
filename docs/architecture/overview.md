# Arquitectura actual

KernelOS es un monolito modular .NET 8. `KernelOS.Core` contiene contratos; `KernelOS.Infrastructure` implementa proveedores y almacenamiento local; `KernelOS.Tools` delimita acciones; `KernelOS.Api` expone HTTP; `KernelOS.Tests` prueba contratos y límites.

```text
HTTP API
 ├─ Chat → IChatModel → OllamaChatModel
 ├─ Planner → KernelPlanner → IToolRouter → Tools
 └─ Filesystem / Document endpoints → IToolRouter
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

Chat, Tool System, Planner determinista de una tarea, Filesystem Read Only, Document Readers para TXT/Markdown/JSON/CSV, Knowledge Core, Memory Core In-Memory, retrieval, Context Builder, RAG Pipeline y Conversation Context están implementados. Son internos: no tienen endpoint ni Tool pública.

Filesystem no accede a rutas no autorizadas. Document Readers reciben referencias autorizadas y el contenido documental es no confiable. Ollama es local en la configuración actual; chat y embeddings usan clientes y modelos separados.

Las siguientes capas no existen todavía y no deben inferirse del diagrama: Kai Agent, Scheduler, Windows Automation, MCP, integraciones de correo/calendario, OCR, Vision, voz y UI. El orden de evolución está en el [Architecture Roadmap](../roadmap/architecture-roadmap.md).
