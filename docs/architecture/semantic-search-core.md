# Semantic Search Core

> Tras un reinicio el índice queda en `NeedsRebuild` hasta que el reindexado explícito reconstruya la familia activa desde Memory durable. Durante la ejecución, el mantenimiento incremental es eventual y puede marcarlo `Dirty` ante una inconsistencia.

Semantic Search recibe un `EmbeddingVector` de consulta, pagina candidatos compatibles desde `IVectorIndex` y ordena referencias por cosine similarity. La fórmula natural es `cos(a,b) = dot(a,b) / (||a|| * ||b||)` en `[-1, 1]`; el score público es `(cos + 1) / 2` en `[0, 1]`. Por tanto, idénticos son 1, ortogonales 0.5 y opuestos 0.

La compatibilidad exige provider, modelo, versión (incluido null solo con null) y dimensiones idénticos. Una query de norma cero es inválida y candidatos de norma cero se omiten. `SemanticSearchOptions` limita candidatos, tamaño de página y TopK; al alcanzar `MaxCandidates` se devuelve `PartialSuccess` con warning. La concurrencia es segura pero no transaccional, heredada del índice en memoria. Los resultados solo incluyen score, identidad de VectorRecord y referencias por ID. Hybrid Search puede degradar de forma controlada si esta rama falla; Semantic Search no selecciona ni crea providers.
