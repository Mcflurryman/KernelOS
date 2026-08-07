# Search Engine Core

Search Engine Core consulta `IMemoryStore` y produce hits a nivel de `MemoryItem`. No modifica Memory, no persiste, no lee archivos y no depende de Readers, Filesystem, LLM, herramientas ni API pública.

La normalización usa Unicode Form C, trim, minúsculas invariant y colapso de espacios; preserva acentos. La tokenización separa letras y dígitos Unicode por espacios o puntuación básica. Las consultas de tokens usan política AND: todos los tokens deben aparecer. Prefix exige que cada token de consulta sea prefijo de algún token del item. Exact compara el contenido normalizado completo.

Los filtros exactos por tipo, documento de Knowledge, documento de Memory y propiedad de metadatos se aplican antes del scoring. Los límites controlan longitud, tokens, candidatos y resultados. El orden es determinista: score descendente, `UpdatedAt` descendente, MemoryDocumentId y MemoryItemId.

El score entero es explicable: exacto +100, prefijo +40, +10 por token exacto, título +5, metadata filtrada +5 y posición inicial +2/+1. Cada componente se expone en `SearchScore`.

El servicio es singleton sin estado mutable por solicitud y depende del singleton `IMemoryStore`. El contenido, incluidos prompt injections, sigue siendo dato no confiable; se conserva procedencia segura y no se registran queries o contenido completos. Embeddings Core y Vector Index Core están separados de Search; Semantic Search y RAG podrán combinar resultados léxicos y semánticos en una capa futura sin cambiar los contratos de Memory.

No existe `SearchTool` ni endpoint: antes se requiere una política de permisos y un Context Builder/RAG estable.
