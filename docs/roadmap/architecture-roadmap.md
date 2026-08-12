# Architecture Roadmap

## Actualización de Retrieval

- Semantic Index Rebuild Foundation desde Memory durable: ✅ (rebuild explícito y atómico por familia; vectors y embeddings siguen volátiles).
- Sync incremental, selección explícita de provider, auto-reindex de arranque y persistencia vectorial: ⬜.

El Execution Audit Trail interno está implementado y validado; no implica persistencia, UI, métricas ni una API pública de audit.

> Kai Planner Orchestration v1 y Architecture / Composition Cleanup: implementados y validados localmente.

Estados: ✅ Implementado · 🟡 En progreso · ⬜ Pendiente · ⚪ Diseñado / reservado.

## Fase 1 — Engineering Foundation

**Objetivo:** cambios revisables y reproducibles. **Dependencia:** ninguna. **Estado:** ✅

- Workflow de ramas, CI, validación local, estándares y ADRs: ✅
- Configuración mediante Options y línea base de pruebas: ✅
- Architecture / Composition Cleanup: ✅

## Fase 2 — Runtime Foundation

**Objetivo:** ejecutar interacciones locales controladas. **Dependencia:** Fase 1. **Estado:** ✅

- `IChatModel` y Ollama Chat provider: ✅
- Tool System, registro y router: ✅
- Planner determinista de una tarea explícita, separado de ejecución: ✅
- Filesystem Capability Read Only y raíces autorizadas: ✅

## Fase 3 — Knowledge Ingestion

**Objetivo:** convertir documentos autorizados en información trazable. **Dependencia:** Filesystem Capability. **Estado:** ✅

- Document Readers para TXT, Markdown, JSON y CSV: ✅
- Knowledge Core determinista: ✅
- Persistence Foundation para Memory durable local: ✅
- PDF, DOCX, XLSX y OCR: ⬜
- Historial de versiones de Memory: ⬜

## Fase 4 — Retrieval

**Objetivo:** recuperar conocimiento sin confundir búsqueda léxica y semántica. **Dependencia:** Knowledge y Memory. **Estado:** 🟡

- Search Engine Core léxico determinista: ✅
- Embeddings Core y Ollama Embeddings Provider local: ✅
- Vector Index: ✅
- Persistencia de Vector Index, embeddings y pipeline de reindexado desde Memory durable: ⬜
- Semantic Search: ✅
- Hybrid Search: ✅

## Fase 5 — Context & Intelligence

**Objetivo:** construir contexto seguro para un Kai Agent. **Dependencia:** Retrieval estable y políticas de acceso. **Estado:** 🟡

- Context Builder y citas/procedencia: ✅
- RAG Pipeline: ✅
- Conversation Context Core (historial reciente por request): ✅
- Kai Agent Core v1 (Chat/RAG; Planner no disponible): ✅
- Política de memoria a largo plazo: ⚪
- Orquestación y razonamiento más allá del Planner determinista: ⚪

## Fase 6 — Automation

**Objetivo:** ejecutar trabajo autorizado y observable. **Dependencia:** Kai Agent, permisos y confirmaciones. **Estado:** 🟡

- Tool Confirmation & Execution Policy: ✅
- Execution Approval / Confirmation Surface: ✅
- Multi-task Authorization Preflight: ✅
- Kai Planner Orchestration: ✅
- Execution Audit Trail interno: ✅
- Task Executor, Scheduler, trabajos en segundo plano y notificaciones: ⚪
- Políticas de permisos y confirmaciones para acciones sensibles: ⚪

## Fase 7 — Integrations

**Objetivo:** integrar sistemas externos mediante fronteras explícitas. **Dependencia:** Automation y seguridad. **Estado:** ⬜

- Windows Automation, navegador, Email, Calendar, Outlook y almacenamiento cloud: ⬜
- MCP: ⬜

## Fase 8 — Multimodal

**Objetivo:** ampliar entradas y salidas sin ejecutar contenido activo. **Dependencia:** ingestion y seguridad. **Estado:** ⬜

- PDF enriquecido, DOCX, XLSX, OCR y Vision: ⬜
- Speech-to-Text y Text-to-Speech: ⬜

## Fase 9 — Productization

**Objetivo:** distribuir y operar el producto localmente. **Dependencia:** capacidades estables. **Estado:** ⬜

- Desktop UI, instalador, actualizaciones, empaquetado y releases: ⬜
- Diagnóstico local, backup y migración: ⬜
