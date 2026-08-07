# Changelog

## [Unreleased]

### Added

- Embeddings Core: contratos independientes de proveedor, normalización, hashes, compatibilidad de vectores y fake determinista solo para pruebas.
- Search Engine Core: búsqueda léxica determinista por exacto, tokens y prefijos sobre Memory, con filtros, score explicable y procedencia segura.
- Memory Core In-Memory: contratos, almacenamiento concurrente, consultas exactas, versionado y pruebas de integración con Knowledge.
- Knowledge Core: modelos, `IKnowledgeBuilder`, transformación determinista, chunking por caracteres, procedencia, hashes y pruebas de integración con Document Readers.

### Fixed

- Resolución portable y segura de aliases especiales de filesystem cuando Desktop o Documents no están disponibles.
- Document Readers Core para TXT, Markdown, JSON y CSV.
- DocumentTool, endpoint `POST /documents/read`, límites configurables y hashes SHA-256.

## [0.4.0] - 2026-08-07

### Added

- API base en .NET 8.
- Chat local mediante Ollama e IChatModel.
- Tool System.
- Planner determinista inicial.
- Filesystem Capability Read Only.
- Seguridad mediante AllowedRoots y Workspace.
- Documentación de arquitectura y ADR.

### Security

- Validación de rutas.
- Bloqueo de escapes mediante `..`.
- Acceso exclusivamente a raíces autorizadas.
- Contenido sensible excluido de logs.
