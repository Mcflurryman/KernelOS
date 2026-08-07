# Product Roadmap

Estados: ✅ Implementado · 🟡 En progreso · ⬜ Pendiente · ⚪ Diseñado / reservado.

## Knowledge

- Leer documentos TXT, Markdown, JSON y CSV dentro de raíces autorizadas: ✅
- Transformar documentos leídos en Knowledge y conservarlo en memoria de proceso: ✅ (interno; sin endpoint de Knowledge/Memory)
- Buscar conocimiento por coincidencia exacta, tokens y prefijos: ✅ (interno; no es búsqueda semántica)
- Buscar documentos por significado y combinar resultados: ✅ (interno; sin endpoint, Tool ni RAG)
- Preparar contexto y citas seguras desde resultados recuperados: ✅ (interno; sin RAG ni Chat)
- Recordar conocimiento tras reiniciar: ⬜

## Assistant

- Conversar localmente con Ollama: ✅
- Ejecutar una tarea explícita mediante el Planner y una Tool registrada: ✅
- Usar contexto recuperado, recordar conversaciones y responder con fuentes: ⬜
- Kai Agent autónomo: ⚪

## Automation e integraciones

- Consultar filesystem en modo Read Only bajo raíces autorizadas: ✅
- Escribir o administrar archivos, programar tareas, controlar Windows y notificar: ⬜
- Calendar, Email, Outlook, navegador, Drive y MCP: ⬜

## Multimodal y producto

- PDF, Word, Excel, OCR, imágenes y voz: ⬜
- Desktop UI, configuración de producto, instalación y actualizaciones: ⬜

Los modelos locales se configuran explícitamente. `embeddinggemma` sirve para generar vectores, pero no habilita por sí solo Vector Index, Semantic Search, Hybrid Search ni RAG.
