# Context Builder Core

Context Builder transforma referencias de Hybrid Search en un `ContextPack` interno. Resuelve exclusivamente cada `MemoryItemId` mediante `IMemoryStore`; no lee archivos, no usa Readers ni consulta Knowledge directamente. Cada item conserva contenido, procedencia segura, score híbrido, orden y una cita `C1`, `C2`, etc.

La selección ordena por score híbrido descendente, conserva el orden de entrada en empates y deduplica defensivamente por `MemoryItemId`. El filtro de score se aplica antes de resolver Memory. Un item desaparecido se omite con `CONTEXT_ITEM_NOT_FOUND`, sin invalidar el resto del pack.

`ContextOptions` limita tokens e items. Los tokens se estiman de forma determinista como `ceil(caracteres / CharactersPerTokenEstimate)` mediante `IContextTokenEstimator`; `CharacterRatioTokenEstimator` usa inicialmente 4 caracteres por token. Es una estimación conservadora reemplazable, no un tokenizer de modelo. Al llegar a MaxItems o cuando el siguiente item completo no cabe, la selección se detiene, marca `Truncated` y devuelve el warning estable correspondiente. No se corta ni resume contenido.

El contenido sigue siendo no confiable: se conserva únicamente como `ContextItem.Content`, nunca como system prompt ni instrucción ejecutable. RAG Pipeline puede consumir el pack mediante su propio prompt builder; Context Builder sigue sin llamar a LLM, endpoints, Tools o Planner. El builder y estimador son singletons sin estado por solicitud; la concurrencia hereda las garantías de `IMemoryStore`.
