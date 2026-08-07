# ADR 0012: Memory Core in-memory

## Estado

Accepted.

## Decisión

KernelOS incorpora `IMemoryStore` en Core e `InMemoryMemoryStore` singleton en Infrastructure. La memoria almacena snapshots de Knowledge, admite operaciones deterministas y no se expone mediante API ni Tool.

## Consecuencias

La memoria se pierde al finalizar el proceso. La unicidad se aplica por `KnowledgeDocumentId`; Update es atómico, incrementa versión y no conserva historial. Una implementación SQLite futura sustituirá solo Infrastructure mediante el mismo contrato, sin introducir embeddings, búsqueda semántica o RAG en esta fase.
