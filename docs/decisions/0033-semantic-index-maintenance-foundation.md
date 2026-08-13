# ADR 0033: Semantic Index Maintenance Foundation

## Estado

Accepted.

## Contexto

Memory es durable en SQLite y VectorIndex es derivado y volátil. El rebuild explícito por familia recupera el índice tras restart, pero las mutaciones posteriores podían dejarlo stale. No es aceptable hacer un rebuild completo por cada cambio ni crear una transacción distribuida entre SQLite, provider de embeddings y VectorIndex.

## Decisión

Memory emite `MemoryMutationCommitted` solo después del commit, con `Previous`/`Current` exactos. El observer es fail-open: Memory sigue siendo authority aunque el mantenimiento falle. Un coordinador in-memory asigna generación monotónica y mantiene `NeedsRebuild`, `Building`, `Maintaining`, `Ready` y `Dirty`; `Ready` exige familia activa conocida y generaciones aplicada/actual alineadas.

Las mutaciones entran en un Channel bounded en memoria de capacidad configurable (256 por defecto) y un worker de un solo consumidor. Changed/new items generan embeddings; unchanged no los regeneran; deletes derivan IDs deterministas. El patch atómico de una familia publica deletes y upserts juntos. Overflow, gap, provider no seleccionable, familia distinta, vector ausente o fallo técnico dejan el estado `Dirty`. El full rebuild sigue siendo la única recuperación hacia `Ready`.

No se implementa outbox durable mientras VectorIndex siga perdiéndose íntegramente al restart.

## Consecuencias

Las escrituras de Memory no esperan Ollama y la consistencia semántica es eventual. Un índice puede estar `Dirty`, y tras restart siempre vuelve a `NeedsRebuild`; el rebuild explícito sigue siendo necesario. El mantenimiento incremental reduce rebuilds durante la ejecución, pero queue y estado se pueden perder en crash/shutdown sin violar el modelo porque el VectorIndex también se pierde. Si VectorIndex pasa a ser durable, se deberá revisar una outbox/watermark durable.
