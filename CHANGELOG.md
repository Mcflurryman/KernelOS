# Changelog

## [Unreleased]

### Added

- Knowledge Core: modelos, `IKnowledgeBuilder`, transformación determinista, chunking por caracteres, procedencia, hashes y pruebas de integración con Document Readers.
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
