# Memory Core

> La captura `IMemorySnapshotProvider` materializa una copia consistente del corpus para el reindexado interno; no sustituye las consultas normales. Véase [Semantic Index Rebuild Foundation](semantic-index-rebuild.md).

Memory Core es el almacén interno durable de `KnowledgeDocument`. Recibe conocimiento ya normalizado y ofrece Store, Update, Delete, Get y consultas exactas; no lee archivos, no conoce Readers, RawDocument, Filesystem, LLM, Planner ni herramientas.

El runtime registra `SqliteMemoryStore` singleton mediante DI. SQLite conserva el agregado completo de forma local, transaccional y durable; su inicialización y límites de seguridad se describen en [Persistence Foundation](persistence-foundation.md). `InMemoryMemoryStore` se conserva para pruebas de contrato, con estructuras concurrentes y actualizaciones compare-and-swap atómicas.

La identidad de `MemoryDocument` es el `KnowledgeDocument.Id`, nunca el hash. Solo se permite un documento por `KnowledgeDocumentId`; un Store duplicado devuelve `AlreadyExists` sin sobrescribir. Update mantiene `CreatedAt`, reemplaza items y metadatos, incrementa versión incluso ante contenido igual y recalcula el hash. No hay historial; Delete es físico y el segundo delete devuelve `NotFound`.

Las consultas de Memory son deterministas y exactas por id, documento de conocimiento, tipo, contenido, hash o propiedad de metadatos. Se ordenan por `UpdatedAt` descendente y después por Id. Search Engine Core consume `IMemoryStore` para búsqueda léxica de items; Memory no implementa ranking. No existen embeddings, búsqueda semántica, RAG ni endpoint o MemoryTool.

Memory preserva exclusivamente procedencia y metadatos seguros de Knowledge. El contenido es dato no confiable y prompt injection sigue siendo texto. No hay historial, endpoint ni Tool. Vector Index y embeddings continúan separados e in-memory; su reindexado desde Memory durable requerirá una decisión posterior.
