# ADR 0034: Persistent Conversation Memory

## Estado

Accepted.

## Decisión

KernelOS persiste conversaciones en un dominio separado dentro de la misma SQLite. Los mensajes se ordenan por secuencia, el User se confirma antes de llamar a Kai y un Assistant visible elegible se confirma después. Los turns se serializan por conversación y el historial durable se limita antes de aplicar Conversation Context.

Chat consume historial; RAG lo consume solo en generación y conserva la query actual para retrieval. Planner no recibe historial. La API dedicada no acepta historial de cliente; `/kai` y `/chat` no cambian. No se persisten resultados raw de Tools ni se indexan conversaciones semánticamente.

## Consecuencias

La continuidad sobrevive reinicios y puede haber turns solo de User ante fallo/cancelación posterior. SQLite local sigue siendo texto plano a nivel de aplicación. Reintentos HTTP pueden duplicar turns hasta introducir idempotencia durable. La correlación entre pending execution y conversación tras ejecutar queda aplazada.
