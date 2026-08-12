# ADR 0032: Hybrid Search Graceful Degradation

## Estado

Accepted.

## Contexto

Hybrid podía devolver `ProviderUnavailable` aunque lexical tuviera contexto útil. Como consecuencia, RAG podía fallar innecesariamente. Kai tampoco traducía correctamente `RagStatus.PartialSuccess`, y un throw inesperado de lexical impedía el fallback semantic-only.

## Decisión

Hybrid ejecuta lexical independientemente de la disponibilidad semantic. Cuando una rama falla técnicamente o no está disponible y la otra devuelve resultados, usa `PartialSuccess`; no se añaden estados `Degraded` ni `HybridSearchMode`. No se selecciona un provider cuando hay cero o más de un generator.

La rama sana se renormaliza a peso efectivo `1.0` solo ante fallo técnico. Si una rama se ejecuta correctamente pero no tiene hits, se mantienen los pesos híbridos normales. La cancelación prevalece. Los diagnósticos se expresan mediante warnings seguros, sin excepciones crudas.

RAG continúa con `PartialSuccess` cuando existen resultados, y Kai preserva `PartialSuccess` y `Cancelled` al traducir la respuesta RAG.

## Consecuencias

RAG puede usar contexto lexical de Memory cuando embeddings o semantic fallen, y puede usar resultados semantic si lexical falla. La resiliencia mejora sin endpoints, Tools ni cambios Core.

La selección explícita entre múltiples providers sigue pendiente. Retrieval permanece secuencial lexical-first y todavía no cuenta con observabilidad estructurada específica. El milestone no modifica el rebuild semántico, sincronización incremental, auto-reindex ni persistencia vectorial.
