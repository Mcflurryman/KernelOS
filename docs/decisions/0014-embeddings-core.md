# ADR 0014: Embeddings Core desacoplado

## Estado

Accepted.

## Decisión

KernelOS define `IEmbeddingGenerator` y modelos de vectores en Core sin proveedor productivo registrado. Los vectores permanecen fuera de Knowledge y Memory; la compatibilidad ordinal exige provider, modelo, versión y dimensiones coincidentes, incluida la distinción entre versión nula y explícita.

## Consecuencias

Todavía no existen embeddings reales. El fake determinista se limita a tests. El próximo milestone implementará un proveedor local, preferentemente Ollama, sin cambiar Core ni consumidores. Un Vector Index separado deberá regenerar vectores cuando cambie un modelo incompatible.
