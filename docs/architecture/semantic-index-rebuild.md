# Semantic Index Rebuild Foundation

Memory SQLite es la fuente durable de verdad. Los embeddings y `InMemoryVectorIndex` son estado derivado, en memoria y no persistido: tras reiniciar, Memory permanece y el índice vectorial queda vacío.

El servicio interno `IVectorReindexService`/`MemoryVectorReindexService` reconstruye explícitamente la familia activa:

```text
SQLite Memory → Memory Snapshot → GenerateBatchAsync
             → shadow VectorRecords → ReplaceFamilyAsync → SemanticSearch
```

`IMemorySnapshotProvider` está separado de `IMemoryStore` porque la captura es específica del indexado. El snapshot se materializa dentro de una única transacción de lectura SQLite, incluye agregados completos, orden determinista, conteos y `CapturedAt`, y es una copia profunda independiente. La conexión se cierra antes de generar embeddings.

La familia se identifica por `VectorFamilyKey(Provider, Model, Version, Dimensions)`. El rebuild toma la familia desde el único `IEmbeddingGenerator` disponible, procesa batches secuenciales de 32 y construye records en sombra. `ReplaceFamilyAsync` es el único commit point: valida el conjunto completo y publica atómicamente solo esa familia. El índice anterior sigue disponible mientras se construye el nuevo; otras familias se conservan.

Un snapshot sin items publica un reemplazo vacío: devuelve `NoMemory` y `Published=true`, porque describe un corpus vacío reconciliado. Ante fallo de snapshot, batch, embedding, validación, límite o cancelación antes del commit, no hay publicación parcial y se conserva la familia anterior. Rebuilds concurrentes devuelven `AlreadyRunning`.

El rebuild es idempotente para el mismo snapshot y familia; cambios o deletes en Memory se reflejan en el siguiente full rebuild. No hay sincronización incremental: una escritura posterior a `CapturedAt` puede dejar el índice eventualmente stale. `CapturedAt` es informativo, no una garantía de ausencia de escrituras posteriores.

## Privacidad y límites v1

No se registran contenido de Memory, textos de entrada, vectores, metadata completa, rutas ni excepciones crudas; solo familia, conteos, estado, duración y códigos seguros. No se emiten eventos de Execution Audit Trail: es mantenimiento interno, no una Tool.

No hay endpoint, Tool, integración Kai, UI, worker/hosted service, auto-rebuild en startup ni persistencia de vectors o embeddings. El servicio exige exactamente un generator: cero o más de uno fallan por indisponibilidad o selección ambigua. El snapshot, resultados de embeddings y records sombra coexisten en memoria; v1 prioriza consistencia y simplicidad sobre optimización de corpus grandes.

Hybrid Search Graceful Degradation no modifica `IMemorySnapshotProvider`, `IVectorReindexService`, `ReplaceFamilyAsync` ni esta persistencia. Solo permite que retrieval continúe con lexical cuando semantic no está disponible o falla.
