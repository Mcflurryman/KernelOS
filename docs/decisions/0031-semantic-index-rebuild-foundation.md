# ADR 0031: Semantic Index Rebuild Foundation

## Estado

Accepted.

## Contexto

Memory ya es durable en SQLite, pero Vector Index y embeddings siguen volátiles. Tras reiniciar, el índice semántico queda vacío. `QueryAsync` paginado no ofrece una vista estable del corpus durante escrituras concurrentes y el índice no disponía de publicación atómica.

## Decisión

KernelOS separa `IMemorySnapshotProvider` de `IMemoryStore` y materializa snapshots completos. `IVectorIndex` incorpora `VectorFamilyKey` y `ReplaceFamilyAsync` para sustituir atómicamente una familia. `MemoryVectorReindexService` reconstruye explícitamente la familia activa del único generator disponible con batches secuenciales, records sombra y all-or-nothing.

`ReplaceFamilyAsync` es el commit point. Antes de él, fallo o cancelación preservan la familia anterior. El rebuild no implementa sync incremental, startup automático ni persistencia de embeddings/vectors.

## Consecuencias

El índice semántico puede reconstruirse tras un reinicio desde Memory durable. El proceso consume tiempo y memoria porque materializa snapshot, embeddings y records sombra. Las escrituras posteriores al snapshot implican consistencia eventual; incremental sync queda para un milestone posterior. Las familias anteriores pueden coexistir y no se limpian automáticamente.
