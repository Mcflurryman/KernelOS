# Changelog

## [Unreleased]

### Added

- Execution Approval Surface: pending executions opacos, confirmación explícita Approve/Reject y ejecución posterior de snapshots aprobados.

- Tool Confirmation & Execution Policy Core: policy determinista, gate, metadata de riesgo y aprobaciones en memoria de un solo uso con expiración y huella de tarea.
- Estados `RequiresConfirmation` y `Denied` para resultados del Planner.
- Separación segura del Planner: `IPlanBuilder` construye planes sin efectos laterales e `IPlanExecutor` valida y ejecuta tareas secuencialmente mediante el router.
- Estado `Planned`, validación completa previa a la ejecución, fail-fast y cancelación de tareas posteriores.
- ADR 0023 y documentación de arquitectura para la frontera entre planificar y ejecutar.
- Conversation Context Core interno: historial por request con selección reciente determinista, sin persistencia ni llamadas a LLM.
- RAG Pipeline Core interno: orquesta Hybrid Search, Context Builder, prompt determinista y `IChatModel` sin Tools, Planner ni fallback cuando no hay contexto.
- Context Builder Core interno: resuelve referencias de retrieval exclusivamente mediante Memory, prepara citas y aplica presupuesto determinista sin truncar contenido ni llamar a un LLM.
- Hybrid Search Core interno: orquestación de búsqueda léxica y semántica, fusión por `MemoryItemId`, normalización de pesos, degradación controlada y orden determinista.
- Semantic Search Core con cosine similarity sobre Vector Index.
- Vector Index Core In-Memory: contratos, snapshots, filtros administrativos y coexistencia de familias de embeddings.
- OllamaEmbeddingGenerator local mediante `/api/embed`, batch nativo, validación dimensional y configuración condicional por proveedor.
- Embeddings Core: contratos independientes de proveedor, normalización, hashes, compatibilidad de vectores y fake determinista solo para pruebas.
- Search Engine Core: búsqueda léxica determinista por exacto, tokens y prefijos sobre Memory, con filtros, score explicable y procedencia segura.
- Memory Core In-Memory: contratos, almacenamiento concurrente, consultas exactas, versionado y pruebas de integración con Knowledge.
- Knowledge Core: modelos, `IKnowledgeBuilder`, transformación determinista, chunking por caracteres, procedencia, hashes y pruebas de integración con Document Readers.

### Fixed

- `GET /health` obtiene la versión de producto desde los metadatos del ensamblado.
- Resolución portable y segura de aliases especiales de filesystem cuando Desktop o Documents no están disponibles.
- Document Readers Core para TXT, Markdown, JSON y CSV.
- DocumentTool, endpoint `POST /documents/read`, límites configurables y hashes SHA-256.

## [0.4.0] - 2026-08-07

### Added

- API base en .NET 8.
- Chat local mediante Ollama e IChatModel.
- Tool System.
- Planner determinista inicial.
- Filesystem Capability Read Only.
- Seguridad mediante AllowedRoots y Workspace.
- Documentación de arquitectura y ADR.

### Security

- Validación de rutas.
- Bloqueo de escapes mediante `..`.
- Acceso exclusivamente a raíces autorizadas.
- Contenido sensible excluido de logs.
