# ADR 0015: Proveedor local Ollama de embeddings

## Estado

Accepted.

## Decisión

KernelOS implementa `OllamaEmbeddingGenerator` en Infrastructure con `/api/embed`, `embeddinggemma` por defecto y batching nativo. Se registra condicionalmente mediante `Embeddings:Provider=ollama`; Chat mantiene su configuración y modelo independientes.

## Consecuencias

Ollama y el modelo deben estar instalados localmente; no hay descarga automática. La dimensión observada se valida frente a `ExpectedDimensions=768` y cualquier diferencia falla de forma segura. No se añaden Vector Index, búsqueda semántica, RAG, endpoint ni Tool.
