# ADR 0019: Context Builder Core

## Estado

Accepted.

## Decisión

Context Builder es una capa interna separada de retrieval y del LLM. Resuelve contenido únicamente a través de `IMemoryStore`, prepara citas sin renderizarlas para un modelo y aplica un presupuesto determinista basado en un estimador de tokens reemplazable.

La primera versión admite solo items completos: no corta, resume ni llama a un LLM. Los resultados duplicados se deduplican por `MemoryItemId`; el contenido recuperado permanece no confiable.

## Consecuencias

No se añaden endpoints, Tools, prompts finales ni RAG. Un tokenizer específico o una política de redundancia por contenido podrán sustituir el estimador o ampliar la selección sin cambiar `IContextBuilder`.
