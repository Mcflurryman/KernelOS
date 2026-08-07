# ADR 0016: Vector Index Core en memoria

## Estado

Accepted.

## Decisión

KernelOS incorpora `IVectorIndex` en Core e `InMemoryVectorIndex` singleton en Infrastructure. Almacena `EmbeddingVector` y referencias por IDs, con CRUD y filtros administrativos, sin métricas de similitud.

## Consecuencias

Memory y Embeddings permanecen independientes; la correlación es explícita por IDs. El índice admite familias de modelos diferentes sin tratarlas como equivalentes. Persistencia, ANN, Semantic Search, Hybrid Search y RAG requieren decisiones posteriores.
