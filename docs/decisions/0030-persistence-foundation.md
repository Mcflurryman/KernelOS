# ADR 0030: Persistence Foundation para Memory

## Estado

Accepted.

## Contexto

Memory era exclusivamente in-memory, por lo que reiniciar KernelOS eliminaba el conocimiento normalizado. Vector, approvals, pending executions y audit tienen semánticas y límites de seguridad distintos; persistirlos automáticamente junto a Memory ampliaría el alcance y cambiaría sus garantías.

## Decisión

KernelOS usa SQLite local mediante `Microsoft.Data.Sqlite` y ADO.NET directo para que Memory sea durable. Las migraciones SQL embebidas son forward-only, transaccionales y se controlan con `schema_version`. El runtime registra `IMemoryStore` como `SqliteMemoryStore`; `InMemoryMemoryStore` permanece disponible para pruebas de contrato.

Vector Index sigue derivado e in-memory, y embeddings no se persisten. Approvals y pending executions siguen volátiles deliberadamente: un reinicio los invalida como comportamiento de seguridad. Execution Audit Trail sigue bounded e in-memory, y Conversation Context continúa limitado a cada request.

## Consecuencias

El conocimiento de Memory sobrevive al reinicio y SQLite pasa a ser una dependencia de runtime. El arranque debe inicializar y migrar la base de manera segura, y las migraciones futuras aumentan la complejidad operacional. Un reindexado vectorial desde Memory durable requerirá un milestone separado; esta decisión no incorpora persistencia de Vector Index, embeddings, conversaciones, audit, approvals ni pending executions.
