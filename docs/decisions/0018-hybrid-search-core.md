# ADR 0018: Hybrid Search Core

## Estado

Accepted.

Hybrid Search se implementa como orquestador interno de `ISearchEngine`, un `IEmbeddingGenerator` explícitamente seleccionado e `ISemanticSearchEngine`. Fusiona resultados duplicados por `MemoryItemId` y no incorpora contexto ni RAG.

Se normalizan los pesos léxico y semántico y se ordena de forma determinista. Una fuente puede degradarse a `PartialSuccess` con avisos, pero no se selecciona un proveedor de embeddings cuando hay más de uno registrado. No se añaden endpoints, Tools ni dependencias nuevas.
