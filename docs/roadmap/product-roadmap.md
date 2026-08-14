# Product Roadmap

Estados: ✅ Implementado · 🟡 En progreso · ⬜ Pendiente · ⚪ Diseñado / reservado.

## Knowledge and assistant

- Leer documentos autorizados TXT, Markdown, JSON y CSV, transformarlos en Knowledge y conservar Memory local: ✅
- Recuperación lexical y semántica, contexto seguro, RAG interno y citas: ✅
- Conversar localmente con Ollama y usar Kai Agent para Chat/RAG/Planner explícito: ✅
- Conversaciones persistentes entre sesiones con historial acotado: ✅
- UI web local para crear, borrar, leer y enviar conversaciones bajo `/ui`: ✅
- Citas y metadata visibles en UI: 🟡 (transitorias, no persistidas)
- Streaming, Markdown, adjuntos y búsqueda/administración visible de Memory/Knowledge: ⬜
- Conversation Pending Correlation + Confirmation Actions: ✅; Pending/Approval siguen volátiles tras restart.

## Actions and automation

- Construir planes y exigir confirmación one-shot para efectos laterales: ✅
- Confirmar/rechazar por API y ejecutar snapshots aprobados: ✅
- Acciones de confirmation desde conversación, agent loop, scheduler y notificaciones: ⬜

## Integrations and product

- Filesystem Read Only bajo raíces autorizadas: ✅
- Escritura de archivos, Windows Automation, navegador, Email, Calendar, Outlook, Drive y MCP: ⬜
- PDF, Office, OCR, imágenes y voz: ⬜
- Autenticación multiusuario, configuración de producto, instalador, actualizaciones y desktop app: ⬜
