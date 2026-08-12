# Product Roadmap

## Actualización de Knowledge

- Reconstruir explícitamente el índice semántico desde Memory durable: ✅ (sin auto-startup ni persistencia vectorial).
- Hardening lexical-only/semantic-only para Hybrid/RAG: ✅ (sin selector de providers).
- Sincronizar automáticamente Memory con el índice semántico: ⬜.

Existe una traza interna de ejecución para observabilidad de dominio; no es una feature visible, persistente ni accesible por endpoint.

> Kai puede iniciar acciones explícitas bajo policy y devolver confirmación pendiente. No hay auto-approval, agent loop, scheduler, memoria conversacional persistente, navegador, Spotify, Maps, trading ni coding agent.

Estados: ✅ Implementado · 🟡 En progreso · ⬜ Pendiente · ⚪ Diseñado / reservado.

## Knowledge

- Leer documentos TXT, Markdown, JSON y CSV dentro de raíces autorizadas: ✅
- Transformar documentos leídos en Knowledge y conservarlo de forma durable en SQLite local: ✅ (interno; sin endpoint de Knowledge/Memory)
- Buscar conocimiento por coincidencia exacta, tokens y prefijos: ✅ (interno; no es búsqueda semántica)
- Buscar documentos por significado y combinar resultados: ✅ (interno; sin endpoint, Tool ni RAG)
- Preparar contexto y citas seguras desde resultados recuperados: ✅ (interno; consumido por RAG interno)
- RAG interno basado en contexto recuperado: ✅ (degradación parcial usable; sin endpoint público)
- Contexto conversacional reciente por request: ✅ (interno; sin persistencia)
- Recordar conocimiento tras reiniciar: ✅ (Memory durable; Vector Index y embeddings se reconstruirán en un milestone futuro)

## Assistant

- Conversar localmente con Ollama: ✅
- Construir un plan explícito sin ejecutarlo y ejecutar un plan validado mediante una Tool registrada: ✅
- Exigir confirmación one-shot para Tools declaradas con efectos laterales: ✅ (Core; sin UI de confirmación)
- Confirmar o rechazar acciones sensibles mediante API y ejecutar el snapshot aprobado: ✅ (sin integración Kai ni UI)
- Autorizar un plan multi-task completo antes de cualquier Tool: ✅ (sin rollback de efectos externos)
- Kai Agent Core v1 para Chat/RAG y Planner explícito: ✅
- Trazabilidad interna de decisiones y ejecuciones: ✅ (sin endpoint, UI ni persistencia)
- Usar contexto recuperado, recordar conversaciones y responder con fuentes: ⬜
- Recordar conversaciones entre sesiones o que Kai recuerde todo lo hablado: ⬜
- Kai Agent autónomo: ⚪

## Automation e integraciones

- Consultar filesystem en modo Read Only bajo raíces autorizadas: ✅
- Escribir o administrar archivos, programar tareas, controlar Windows y notificar: ⬜
- Calendar, Email, Outlook, navegador, Drive y MCP: ⬜

## Multimodal y producto

- PDF, Word, Excel, OCR, imágenes y voz: ⬜
- Desktop UI, configuración de producto, instalación y actualizaciones: ⬜

Los modelos locales se configuran explícitamente. `embeddinggemma` sirve para generar vectores, pero no habilita por sí solo Vector Index, Semantic Search, Hybrid Search ni RAG.
