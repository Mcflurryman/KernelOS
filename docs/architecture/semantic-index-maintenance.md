# Semantic Index Maintenance Foundation

Memory SQLite es la fuente durable de verdad. `InMemoryVectorIndex` y los embeddings son estado derivado, volátil y reconstruible. El mantenimiento incremental reduce rebuilds durante la vida del proceso, pero no convierte el índice semántico en una fuente de verdad ni introduce una transacción distribuida.

```text
Memory transaction → COMMIT → committed mutation → generation → bounded Channel
                                                        ↓
                                             single maintenance worker
                                                        ↓
                                      embedding diff → ApplyFamilyPatchAsync
                                                        ↓
                                  AppliedGeneration / semantic index state
```

Solo una mutación durable genera mantenimiento: `Created` tiene `Previous=null` y `Current`; `Updated` contiene el agregado anterior y el committed exactos; `Deleted` contiene `Previous` y `Current=null`. Los snapshots se capturan dentro de la misma transacción SQLite o compuerta in-memory, nunca mediante un `Get` previo externo. `AlreadyExists`, `NotFound`, solicitud inválida, cancelación antes de commit y fallo durable no emiten mutación.

El observer corre después de commit, con cancelación independiente del request y fail-open: fallo de queue, provider, patch o worker nunca revierte ni convierte en fallida una escritura de Memory ya durable. No registra documentos, contenido, metadata, textos de embedding ni vectores.

## Estado y generaciones

El coordinador publica snapshots coherentes con `CurrentGeneration`, `AppliedGeneration` y `ReadyFamily`.

| Estado | Significado |
| --- | --- |
| `NeedsRebuild` | Estado inicial: el índice volátil no tiene baseline. El worker drena sin crear índice parcial. |
| `Maintaining` | Existe baseline y hay mutaciones contiguas pendientes de patch. |
| `Ready` | Baseline completa de `ReadyFamily` y `AppliedGeneration == CurrentGeneration`. |
| `Dirty` | El índice puede no representar Memory completa; solo un rebuild puede recuperar `Ready`. |
| `Building` | Hay rebuild en curso; el worker no publica patches. |

`CurrentGeneration` cuenta mutaciones committed conocidas. `AppliedGeneration` es la mayor generación completamente representada por la familia activa. Eventos con generación menor o igual a la aplicada son stale y se descartan. Un evento mayor que `AppliedGeneration + 1`, desbordamiento, fallo de embedding/patch, provider ambiguo, familia distinta o vector invariant ausente marca `Dirty`; trabajo posterior se drena sin embeddings ni patch.

`ReadyFamily` contiene provider, modelo, versión y dimensiones. El worker solo opera con exactamente un generator y solo si su familia coincide con la baseline. Un cambio de familia requiere rebuild; no se crea una familia nueva parcial.

## Queue y worker

`SemanticIndexMaintenance:QueueCapacity` tiene valor por defecto 256 y se valida al arrancar. El producer incrementa generación y usa `TryWrite`; no espera Ollama ni usa `Task.Run`. Si la cola está llena no usa `DropOldest`: descarta ese evento, marca `Dirty` y deja Memory en `Success`.

El Channel tiene un único reader. El worker procesa mutations secuencialmente para conservar orden operacional, usa batches secuenciales de 32 y no implementa retries ni coalescing. Create embeddea todos los items nuevos; Update compara `MemoryItem.Id` y `ContentHash`; changed/new se embeddean, removed se borran y unchanged no regeneran embedding. Si un unchanged esperado no tiene vector determinista bajo una baseline, se marca `Dirty`. Delete deriva IDs desde `Previous` y no embeddea.

Rebuild e incremental comparten validación de outputs de embeddings, identidad determinista y construcción de `VectorRecord`. `ApplyFamilyPatchAsync` valida en sombra, aplica deletes y upserts por familia mediante copy-on-write y publica una única referencia; otras familias permanecen intactas y fallo/cancelación conserva el estado anterior.

## Rebuild, restart y límites

El rebuild captura una generación antes de su snapshot. Las mutaciones continúan durante la construcción; si la generación cambia, el rebuild puede publicar el snapshot pero queda `Dirty`, evitando falso `Ready`. Un rebuild fallido o cancelado restaura el estado previo solo si no hubo mutaciones; de lo contrario deja `Dirty`.

Tras restart desaparecen queue, coordinator y VectorIndex mientras Memory permanece: el estado es `NeedsRebuild` y el rebuild explícito sigue siendo necesario. No hay auto-rebuild, endpoint, Tool, Kai, UI, scheduler, outbox durable, vectores/embeddings persistentes ni selección de providers. Una outbox durable no aporta recuperación suficiente mientras el VectorIndex completo sigue siendo volátil; deberá reevaluarse si se hace durable.

La consistencia es eventual dentro del proceso. Retrieval no se bloquea aún cuando el estado es `Dirty`; Hybrid puede seguir degradando a la rama lexical durable. No se emite Execution Audit Trail para este mantenimiento interno.
