# Memory Core

Memory Core es el almacén interno en proceso de `KnowledgeDocument`. Recibe conocimiento ya normalizado y ofrece Store, Update, Delete, Get y consultas exactas; no lee archivos, no conoce Readers, RawDocument, Filesystem, LLM, Planner ni herramientas.

`InMemoryMemoryStore` es singleton mediante DI, porque representa una única memoria de proceso. Usa estructuras concurrentes, una compuerta asíncrona limitada al alta para preservar capacidad e índices, y actualizaciones compare-and-swap atómicas. No persiste datos ni registra contenido.

La identidad de `MemoryDocument` es el `KnowledgeDocument.Id`, nunca el hash. Solo se permite un documento por `KnowledgeDocumentId`; un Store duplicado devuelve `AlreadyExists` sin sobrescribir. Update mantiene `CreatedAt`, reemplaza items y metadatos, incrementa versión incluso ante contenido igual y recalcula el hash. No hay historial; Delete es físico y el segundo delete devuelve `NotFound`.

Las consultas son deterministas y exactas por id, documento de conocimiento, tipo, contenido, hash o propiedad de metadatos. Se ordenan por `UpdatedAt` descendente y después por Id. No existen búsquedas fuzzy, texto completo, embeddings, ranking, RAG ni endpoint o MemoryTool.

Memory preserva exclusivamente procedencia y metadatos seguros de Knowledge. El contenido es dato no confiable y prompt injection sigue siendo texto. El reemplazo futuro por SQLite u otro repositorio implementará `IMemoryStore` sin cambiar los contratos de Core; persistencia, versionado histórico, embeddings, Search y RAG requerirán decisiones propias.
