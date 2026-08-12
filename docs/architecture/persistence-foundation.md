# Persistence Foundation

Memory es la fuente durable de conocimiento normalizado de KernelOS. `KnowledgeDocument` llega desde Knowledge Core y `IMemoryStore` conserva el agregado `MemoryDocument`; no se expone un endpoint ni una Tool de Memory. En la creación actual, `MemoryDocument.Id` y `MemoryDocument.KnowledgeDocumentId` son ambos `KnowledgeDocument.Id`.

Core mantiene contratos y modelos sin conocer SQLite. Infrastructure implementa el runtime con `SqliteMemoryStore`, `Microsoft.Data.Sqlite` y ADO.NET directo; API solo compone el módulo mediante DI. `InMemoryMemoryStore` se conserva para pruebas de contrato. Vector Index, embeddings, approvals, pending executions, Audit Trail y Conversation Context no participan en esta base de datos.

## Almacenamiento y arranque

Por defecto la base local vive en el directorio de datos de aplicación del usuario bajo `KernelOS`; `Persistence:DataDirectory` permite configurarlo. `Persistence:DatabaseFile` debe ser un nombre de archivo simple, no una ruta ni un escape, y siempre se resuelve dentro de ese directorio. La ruta y la cadena de conexión no se exponen en respuestas ni logs normales.

Un hosted service inicializa la base al arrancar. Las migraciones SQL están embebidas, se aplican solo hacia delante y dentro de una transacción. `schema_version` identifica la versión instalada; una base más nueva falla de forma segura. La corrupción no se borra ni recrea automáticamente. El runtime habilita WAL, `foreign_keys`, `busy_timeout` y `synchronous=FULL`.

## Agregado y consultas

Store, Update y Delete son transacciones atómicas del agregado completo: documento, metadata e items. Update conserva `CreatedAt`, reemplaza metadata e items e incrementa la versión; Delete usa las claves foráneas con cascade. No existe transacción conjunta entre Memory y embeddings o Vector Index.

Get y Query entregan snapshots independientes. Query filtra de forma exacta por los campos de `MemoryQuery`, aplica todas las condiciones como AND, ordena por `UpdatedAt` descendente e `Id` ascendente y después aplica `Limit` y `Offset`. La igualdad de contenido, hash y metadata es sensible a mayúsculas/minúsculas.

Las escrituras usan `BEGIN IMMEDIATE`, por lo que actualizaciones concurrentes se serializan sin mezclar agregados; los lectores ven el snapshot anterior completo o el nuevo completo. La durabilidad, rollbacks de create/update, integridad referencial, concurrencia y aislamiento de snapshots se prueban con SQLite real. Los contract tests mantienen paridad observable con `InMemoryMemoryStore` para Store, Get, Update, Delete y Query.

## Seguridad y límites

Todos los valores dinámicos de SQL usan parámetros. Las migraciones son SQL estático embebido. Los errores del proveedor se traducen a resultados seguros (`AlreadyExists` para violaciones de unicidad aplicables y `Failed` para el resto) sin devolver mensajes de SQLite. Content, metadata, referencias, parámetros SQL, cadenas de conexión y rutas sensibles no se registran normalmente.

SQLite v1 no aporta cifrado a nivel de aplicación: la protección depende del perfil y permisos locales de la persona usuaria. Backup no forma parte de este milestone; un backup futuro deberá emplear SQLite Backup API o un snapshot/checkpoint coherente, no copiar el archivo `.db` en caliente. Tampoco se persisten Vector Index, embeddings, approvals, pending executions, Audit Trail ni historial conversacional. El futuro reindexado vectorial partirá de Memory durable como milestone independiente.
