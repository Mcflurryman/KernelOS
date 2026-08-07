# ADR 0013: Search Engine Core léxico determinista

## Estado

Accepted.

## Decisión

KernelOS incorpora `ISearchEngine` en Core y `MemorySearchEngine` singleton en Infrastructure. Consulta únicamente `IMemoryStore`, busca items con coincidencia exacta, tokens AND y prefijos, y aplica ranking entero determinista.

## Consecuencias

Search no persiste, no modifica Memory ni se expone por API o Tool. Unicode Form C conserva acentos y la puntuación solo delimita tokens. Semantic search, embeddings, vector database y RAG siguen fuera de alcance y podrán añadirse como una capa complementaria.
