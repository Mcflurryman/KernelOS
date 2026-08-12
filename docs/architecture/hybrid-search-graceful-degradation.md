# Hybrid Search Graceful Degradation

Hybrid Search consulta dos ramas independientes: lexical sobre Memory y semantic mediante un embedding de consulta y Vector Index. El problema previo era que la ausencia o fallo del provider de embeddings podía terminar Hybrid antes de aprovechar contexto lexical válido; RAG quedaba bloqueado aunque Memory durable tuviera resultados útiles.

## Flujo y estados

Hybrid ejecuta lexical primero. Después intenta usar exactamente un `IEmbeddingGenerator`, generar el embedding y consultar Semantic Search. No selecciona arbitrariamente un provider: cero o más de un generator hacen que semantic no esté disponible para Hybrid v1.

| Lexical | Semantic | Resultado |
|---|---|---|
| hits | hits o vacío sano | `Success` |
| hits | no disponible o fallo técnico | `PartialSuccess` lexical-only |
| fallo técnico | hits | `PartialSuccess` semantic-only |
| ambas sanas vacías | `NoResults` |
| ambas técnicamente fallidas | `Failed` |
| cancelación | cualquier combinación | `Cancelled` |

`PartialSuccess` describe retrieval utilizable, no una respuesta inválida: una rama falló o no estuvo disponible y la otra produjo resultados. `NoResults` sigue siendo correcto cuando lexical funcionó pero no tuvo hits y semantic no estuvo disponible: se adjunta un warning seguro para distinguirlo de ambas ramas sanas vacías. `Failed` indica que no quedó ninguna fuente utilizable tras fallos técnicos. La cancelación prevalece sobre cualquier resultado parcial.

Una rama vacía no equivale a una rama rota. Si semantic se ejecuta correctamente y no encuentra hits, se conservan los pesos híbridos normales. Solo un fallo técnico o indisponibilidad activa el fallback y la renormalización.

## Scores y fusión

Con ambas ramas sanas se mantienen `LexicalWeight` y `SemanticWeight`, normalizados como antes. Si semantic falla técnicamente, lexical usa peso efectivo `1.0`; si lexical falla técnicamente, semantic usa peso efectivo `1.0`. Esto evita penalizar contexto válido por una rama caída. La fusión por `MemoryItemId`, deduplicación y orden determinista no cambian. `MinimumHybridScore` se evalúa después de construir el score final.

## RAG y Kai

Un `HybridSearchStatus.PartialSuccess` con resultados pasa a Context Builder y al modelo; RAG devuelve `RagStatus.PartialSuccess` con sus citas, warnings y modelo. `NoResults` se convierte en `RagStatus.NoContext` y no llama al modelo. `Failed` y `Cancelled` son terminales.

Kai conserva un RAG parcial como `KaiStatus.PartialSuccess`, `ModeUsed=Rag`, y preserva respuesta, citas, warnings y modelo. En modo Auto no cae a Chat ante un parcial; el fallback a Chat sigue reservado para `NoContext`. `RagStatus.Cancelled` se traduce a `KaiStatus.Cancelled`, que la API existente devuelve como HTTP 499.

## Privacidad y límites v1

Los warnings usan códigos seguros: `HYBRID_SEMANTIC_PROVIDER_UNAVAILABLE`, `HYBRID_SEMANTIC_EMBEDDING_FAILED`, `HYBRID_SEMANTIC_SEARCH_FAILED` y `HYBRID_LEXICAL_FAILED`. No incluyen query, contenido recuperado, Memory, embeddings, vectores, contexto RAG ni excepciones crudas. Retrieval no emite Execution Audit Trail porque no es ejecución de una Tool.

No se añaden endpoint, Tool, UI, configuración ni selector de providers. Hybrid continúa lexical-first y secuencial; ejecutar lexical incluso cuando semantic pudiera bastar es un coste aceptado en v1 para conservar corrección y cancelación simple. Este cambio no modifica `IMemorySnapshotProvider`, `IVectorReindexService`, `ReplaceFamilyAsync` ni la persistencia de Memory.
