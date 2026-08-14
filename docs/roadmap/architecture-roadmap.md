# Architecture Roadmap

Estados: ✅ Implementado · 🟡 En progreso · ⬜ Pendiente · ⚪ Diseñado / reservado.

## Foundation

- Engineering workflow, configuración, ADRs y composition root modular: ✅
- Runtime local: chat, Tool System, Planner y Filesystem Read Only: ✅
- Document Readers, Knowledge y Memory durable SQLite: ✅

## Retrieval and intelligence

- Search lexical, embeddings, Vector Index, Semantic Search y Hybrid con degradación controlada: ✅
- Context Builder, RAG, Conversation Context y Kai Agent Core v1: ✅
- Persistencia de Vector Index/embeddings y auto-reindex de inicio: ⬜

## Conversations and UI

- Persistent Conversation Memory: SQLite, API, turns serializados, historial acotado y paginación por secuencia: ✅
- UI Foundation: Blazor WebAssembly alojado por KernelOS.Api bajo `/ui`, same-origin, deep links, conversaciones, health y feedback seguro de turns: ✅
- Idempotencia durable, títulos, indexación semántica de conversaciones y correlación pending/conversation persistente: ⬜
- Streaming, Markdown, adjuntos, Tools UI, Memory UI y autenticación: ⬜
- Conversation Pending Correlation + Confirmation Actions: ✅ (sin Pending/Approval durable ni historial terminal durable).

## Automation

- Tool Confirmation & Execution Policy, Approval Surface, preflight multi-task, Kai Planner Orchestration y Audit Trail interno: ✅
- Scheduler, workers, notificaciones, persistencia de approvals/pending/audit y reanudación: ⬜

## Integrations and productization

- Windows Automation, navegador, Email, Calendar, cloud y MCP: ⬜
- PDF enriquecido, DOCX, XLSX, OCR, Vision y voz: ⬜
- Instalador, actualizaciones, backup/migración, configuración de producto y UI administrativa: ⬜
