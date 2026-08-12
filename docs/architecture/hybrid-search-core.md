# Hybrid Search Core

Hybrid Search combina resultados léxicos y semánticos sin modificar sus motores. Recibe una query, la entrega al único `IEmbeddingGenerator` registrado y consulta `ISearchEngine` e `ISemanticSearchEngine` con el mismo presupuesto de candidatos. No genera ni persiste conocimiento y no construye contexto. Memory durable no vuelve durable Vector Index ni embeddings.

Los resultados se fusionan por `MemoryItemId`; un resultado semántico sin esa referencia conserva como identidad su `VectorRecordId`. El score es la suma ponderada de score léxico normalizado y score semántico, con pesos normalizados para permitir configuración no unitaria. El orden es score híbrido descendente, score semántico descendente, score léxico descendente e ID ascendente.

Solo hay un proveedor de embeddings explícito. Sin proveedor se devuelve `ProviderUnavailable`; con varios se rechaza la operación para no escoger uno de forma implícita. Si una fuente falla, la otra puede devolver resultados con `PartialSuccess` y avisos; si ambas fallan se devuelve `Failed`. La cancelación se propaga sin llamar a fuentes posteriores. Sus referencias las consume Context Builder mediante `IMemoryStore`; RAG Pipeline las usa después mediante contratos, sin que Hybrid Search construya prompts, llame al modelo o exponga endpoint o Tool.
