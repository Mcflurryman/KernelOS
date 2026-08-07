# Vector Index Core

Vector Index Core almacena `EmbeddingVector` ya generados junto a referencias por ID. Es una capa interna independiente: no genera embeddings, no llama a Ollama, no modifica Memory, no interpreta contenido y no calcula similitud.

`InMemoryVectorIndex` mantiene registros en proceso y admite varias familias incompatibles. La identidad única es `InputId + Provider + Model + Version + Dimensions`; un mismo input puede coexistir con modelos, versiones o dimensiones distintas. `QueryAsync` solo filtra administrativamente por referencias, familia, hash o metadata y ordena por `UpdatedAt` descendente e Id ascendente.

Las escrituras usan una compuerta para mantener coherentes el almacén principal y la identidad secundaria. Get y Query devuelven snapshots con metadata copiada; la enumeración concurrente es segura, no una transacción global. Contains y Count lanzan cancelación porque sus retornos simples no llevan estado; las demás operaciones devuelven `Cancelled`.

No hay persistencia, Vector Index remoto, ANN, similitud, Semantic Search, Hybrid Search, RAG, endpoint ni Tool. Un índice persistente futuro podrá implementar `IVectorIndex` y Semantic Search deberá elegir una única familia compatible antes de comparar vectores.
