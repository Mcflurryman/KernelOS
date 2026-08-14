# Persistent Conversation Memory

Conversation es un dominio durable separado de Knowledge Memory. Comparte SQLite local, pero usa `conversations` y `conversation_messages`; no crea `MemoryDocument`, embeddings, Vector Index ni mantenimiento semántico.

`SqliteConversationStore` es la fuente de verdad. Cada conversación comienza en versión 1; cada append confirmado incrementa la versión y recibe una secuencia positiva única por conversación. La migración 002 añade las tablas, FK con cascade, roles controlados, unicidad `(conversation_id, sequence_number)` e índice de lectura paginada.

`POST /conversations/{id}/messages` usa `IConversationTurnOrchestrator`: persiste el User, carga una ventana SQL anterior al User actual, llama a Kai y persiste solo una respuesta Assistant elegible y visible. Un gate por `ConversationId` serializa turns de la misma conversación. Si Kai falla o se cancela después del User, queda un turn user-only; no hay replay automático. Un fallo al persistir Assistant conserva la respuesta y devuelve éxito parcial.

Conversation Context aplica el presupuesto final. Chat recibe roles preservados e historial separado del mensaje actual. RAG usa la query actual exclusivamente para retrieval y usa historial solo para generación. Planner no recibe historial: contexto nunca concede autoridad.

La API expone create/list/get/messages/send/delete. KernelOS.Web consume esta API desde `/ui` y reconcilia el historial durable desde SQLite tras cada turn. Las respuestas parciales no persistidas, citas, metadata y confirmations son estado transitorio de UI. `/kai` y `/chat` permanecen stateless.

El contenido se guarda en SQLite local sin cifrado a nivel de aplicación; los logs y Audit Trail no almacenan conversación. La identidad Conversation↔Pending se conserva separadamente para recuperar confirmation UI, pero Pending/Approval siguen runtime-only y una correlación tras restart es `Unavailable`. No hay idempotencia durable, títulos, indexación semántica, promoción a Knowledge, streaming ni Markdown.
