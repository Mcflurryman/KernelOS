# Embeddings Core

Embeddings Core define contratos independientes de proveedor para transformar texto en vectores. `IEmbeddingGenerator` recibe texto e identifica cada vector por `InputId`; no conoce Knowledge, Memory, filesystem, Readers, Search, herramientas ni LLM.

La normalización compartida aplica Unicode Form C, CRLF/CR a LF y trim exterior. Conserva mayúsculas, acentos y puntuación. SHA-256 se calcula sobre exactamente ese texto normalizado; sirve para cache y obsolescencia, nunca como autorización o identidad permanente.

Los vectores copian sus valores al construirse y exponen una colección de solo lectura; rechazan vacío, dimensiones incoherentes, NaN e infinitos. Dos vectores solo serán comparables si provider, modelo, versión y dimensiones coinciden con comparación ordinal; versión nula solo es compatible con versión nula. Knowledge y Memory no almacenan `float[]`: la futura relación con un Vector Index será por `KnowledgeItemId` o `MemoryItemId` como `EmbeddingInput.Id`.

`OllamaEmbeddingGenerator` es el primer proveedor local y se registra solo cuando `Embeddings:Provider=ollama`; `none` o vacío conserva el arranque sin generador. El fake determinista existe exclusivamente en tests. Un proveedor remoto requerirá decisión y configuración explícitas por privacidad.

Batching conserva orden e IDs, rechaza IDs duplicados como `InvalidInput`, admite resultados parciales explícitos y respeta límites/cancelación. No existe Vector Index, similitud, semantic search, RAG, API o Tool. Un índice futuro tendrá ciclo de vida separado para poder regenerar vectores al cambiar de modelo incompatible.
