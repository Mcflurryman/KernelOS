# Vector Index Core

> `ReplaceFamilyAsync` reconstruye una familia completa y `ApplyFamilyPatchAsync` publica deletes y upserts incrementales de forma atómica. Ninguno hace durables vectors ni embeddings.

Vector Index Core almacena `EmbeddingVector` ya generados junto a referencias por ID. Es una capa interna independiente: no genera embeddings, no llama a Ollama, no modifica Memory, no interpreta contenido y no calcula similitud.

`InMemoryVectorIndex` mantiene registros en proceso y admite varias familias incompatibles. La identidad única es `InputId + Provider + Model + Version + Dimensions`; un mismo input puede coexistir con modelos, versiones o dimensiones distintas. `QueryAsync` solo filtra administrativamente por referencias, familia, hash o metadata y ordena por `UpdatedAt` descendente e Id ascendente.

Las escrituras usan una compuerta para mantener coherentes el almacén principal y la identidad secundaria. Get y Query devuelven snapshots con metadata copiada; la enumeración concurrente es segura, no una transacción global. Contains y Count lanzan cancelación porque sus retornos simples no llevan estado; las demás operaciones devuelven `Cancelled`.

`ApplyFamilyPatchAsync` valida el patch en sombra y publica una única referencia mediante copy-on-write; un fallo o cancelación conserva la familia anterior. Vector Index sigue in-memory y derivado: Persistence Foundation no hace durables vector records ni embeddings. El mantenimiento incremental solo opera sobre una baseline de familia compatible; tras restart o `Dirty`, el rebuild explícito vuelve a ser necesario. No hay Vector Index remoto, ANN, endpoint ni Tool.
