# Vector Index Core

> `ReplaceFamilyAsync` publica atómicamente un conjunto validado de una familia y preserva las demás. El reindexado explícito sigue sin hacer durables vectors ni embeddings.

Vector Index Core almacena `EmbeddingVector` ya generados junto a referencias por ID. Es una capa interna independiente: no genera embeddings, no llama a Ollama, no modifica Memory, no interpreta contenido y no calcula similitud.

`InMemoryVectorIndex` mantiene registros en proceso y admite varias familias incompatibles. La identidad única es `InputId + Provider + Model + Version + Dimensions`; un mismo input puede coexistir con modelos, versiones o dimensiones distintas. `QueryAsync` solo filtra administrativamente por referencias, familia, hash o metadata y ordena por `UpdatedAt` descendente e Id ascendente.

Las escrituras usan una compuerta para mantener coherentes el almacén principal y la identidad secundaria. Get y Query devuelven snapshots con metadata copiada; la enumeración concurrente es segura, no una transacción global. Contains y Count lanzan cancelación porque sus retornos simples no llevan estado; las demás operaciones devuelven `Cancelled`.

Vector Index sigue in-memory y derivado: Persistence Foundation no hace durables vector records ni embeddings. Un futuro milestone podrá reconstruir o reindexar el índice desde Memory durable y, si procede, implementar un `IVectorIndex` persistente. No hay Vector Index remoto, ANN, endpoint ni Tool.
